using System.Net.Http.Json;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public sealed class MorningBriefFunction(
    IFeedReader feedReader,
    IStoryClusterer clusterer,
    ISentStoryStore sentStore,
    IBriefWriter briefWriter,
    IHttpClientFactory httpClientFactory,
    ILogger<MorningBriefFunction> logger)
{
    private const double DuplicateThreshold = 0.40;

    [Function(nameof(MorningBriefFunction))]
    public Task Run([TimerTrigger("0 0 6 * * *")] TimerInfo timer, CancellationToken ct) => Execute(ct);

    public async Task Execute(CancellationToken ct)
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
            ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN not configured.");
        var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
            ?? throw new InvalidOperationException("TELEGRAM_CHAT_ID not configured.");

        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var items = await feedReader.FetchAsync(since, ct);

        logger.LogInformation("Fetched {Count} items across {Sources} sources.",
            items.Count, items.Select(i => i.Source).Distinct().Count());

        if (items.Count == 0)
        {
            logger.LogWarning("No items fetched; nothing to send.");
            return;
        }

        var clusters = clusterer.Cluster(items)
            .Where(c => c.SourceCount >= 2)
            .OrderByDescending(c => c.SourceCount)
            .ThenByDescending(c => c.Primary.Published)
            .Take(8)
            .ToList();

        logger.LogInformation("Clustered {Items} items into {Clusters} stories.", items.Count, clusters.Count);

        if (clusters.Count == 0)
        {
            logger.LogWarning("No multi-source stories in window; nothing to send.");
            return;
        }

        var seen = await sentStore.GetRecentAsync(DateTimeOffset.UtcNow.AddDays(-3), ct);

        var fresh = clusters
            .Where(c => !seen.Any(s => StoryClusterer.Jaccard(c.Tokens, s) >= DuplicateThreshold))
            .ToList();

        logger.LogInformation("{Fresh} of {Total} stories are new.", fresh.Count, clusters.Count);

        if (fresh.Count == 0)
        {
            logger.LogInformation("Nothing new since last brief.");
            return;
        }

        var stories = await briefWriter.WriteAsync(fresh, ct);
        var text = Format(stories);

        var client = httpClientFactory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text, parse_mode = "HTML", disable_web_page_preview = true },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Telegram send failed: {Status} {Body}",
                response.StatusCode, await response.Content.ReadAsStringAsync(ct));
            return;
        }

        await sentStore.RecordAsync(fresh, ct);
        logger.LogInformation("Brief delivered.");
    }

    private static string Format(IReadOnlyList<BriefStory> stories)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>MORNING BRIEF</b> · {DateTimeOffset.UtcNow:dddd, d MMMM}");

        foreach (var (story, n) in stories.Select((s, i) => (s, i + 1)))
        {
            sb.AppendLine();
            sb.AppendLine($"<b>{n}. {Escape(story.Headline)}</b>");

            if (story.Summary.Length > 0)
                sb.AppendLine(Escape(story.Summary));

            if (story.Significance.Length > 0)
                sb.AppendLine($"<i>{Escape(story.Significance)}</i>");

            sb.AppendLine($"{story.SourceCount} sources · <a href=\"{story.Link}\">read</a>");
        }

        var text = sb.ToString();
        return text.Length > 4000 ? text[..4000] + "…" : text;
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}