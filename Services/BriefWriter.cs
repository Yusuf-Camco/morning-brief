using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public sealed class BriefWriter(IHttpClientFactory httpClientFactory, ILogger<BriefWriter> logger) : IBriefWriter
{
    private const string Model = "gemini-3.5-flash-lite";
    private const string SystemPrompt = """
        You compress news headlines into a morning brief.

        You will receive numbered stories, each with headlines from one or more outlets.
        Work ONLY from the text provided. Do not add facts, figures, names, casualty
        counts or context from your own knowledge — if the headlines do not say it, it
        does not go in the brief.

        For each story write:
        - headline: a clear factual headline, under 90 characters
        - summary: one sentence stating what happened, drawn only from the input
        - significance: one sentence on why it matters. If the input does not support a
          claim about significance, describe what to watch instead.

        Order the stories by genuine importance to a reader tracking world affairs,
        business and conflict. Wide coverage is not importance: a widely syndicated
        novelty story ranks below a central bank rate decision or a shift in a military
        alliance. Include every story.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<BriefStory>> WriteAsync(IReadOnlyList<StoryCluster> clusters, CancellationToken ct)
    {
        if (clusters.Count == 0) return [];

        var input = string.Join("\n\n", clusters.Select((c, i) =>
            $"[{i}] {string.Join(" | ", c.Sources.Select(s => s.Title).Distinct())}\n" +
            $"carried by: {string.Join(", ", c.Sources.Select(s => s.Source).Distinct())}"));

        var client = httpClientFactory.CreateClient(nameof(BriefWriter));

        var request = new
        {
            system_instruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[] { new { parts = new[] { new { text = input } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            index = new { type = "INTEGER" },
                            headline = new { type = "STRING" },
                            summary = new { type = "STRING" },
                            significance = new { type = "STRING" }
                        },
                        required = new[] { "index", "headline", "summary", "significance" }
                    }
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Gemini call failed: {Status} {Body}",
                response.StatusCode, await response.Content.ReadAsStringAsync(ct));
            return Fallback(clusters);
        }

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? throw new JsonException("No text in response.");

            var written = JsonSerializer.Deserialize<List<WrittenStory>>(text, JsonOptions)
                ?? throw new JsonException("Null deserialization result.");

            var stories = written
                .Where(w => w.Index >= 0 && w.Index < clusters.Count)
                .Select(w => new BriefStory(
                    w.Headline, w.Summary, w.Significance,
                    clusters[w.Index].Primary.Link,
                    clusters[w.Index].SourceCount))
                .ToList();

            logger.LogInformation("Model returned {Count} of {Expected} stories.", stories.Count, clusters.Count);
            return stories.Count > 0 ? stories : Fallback(clusters);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not parse model output; falling back to raw headlines.");
            return Fallback(clusters);
        }
    }

    private static IReadOnlyList<BriefStory> Fallback(IReadOnlyList<StoryCluster> clusters) =>
        clusters.Select(c => new BriefStory(c.Primary.Title, "", "", c.Primary.Link, c.SourceCount)).ToList();

    private sealed record WrittenStory(int Index, string Headline, string Summary, string Significance);
}