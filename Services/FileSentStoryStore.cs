using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public sealed class FileSentStoryStore(ILogger<FileSentStoryStore> logger) : ISentStoryStore
{
    private const string Path = "state/sent-stories.json";

    public async Task<IReadOnlyList<HashSet<string>>> GetRecentAsync(DateTimeOffset since, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(Path))
            {
                logger.LogInformation("No state file; treating all stories as new.");
                return [];
            }

            var records = JsonSerializer.Deserialize<List<Record>>(await File.ReadAllTextAsync(Path, ct)) ?? [];

            var recent = records
                .Where(r => r.SentAt >= since)
                .Select(r => r.Tokens.ToHashSet())
                .ToList();

            logger.LogInformation("Loaded {Count} previously sent stories.", recent.Count);
            return recent;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not read state file; proceeding without deduplication.");
            return [];
        }
    }

    public async Task RecordAsync(IReadOnlyList<StoryCluster> clusters, CancellationToken ct)
    {
        try
        {
            var existing = File.Exists(Path)
                ? JsonSerializer.Deserialize<List<Record>>(await File.ReadAllTextAsync(Path, ct)) ?? []
                : [];

            var cutoff = DateTimeOffset.UtcNow.AddDays(-7);

            var updated = existing
                .Where(r => r.SentAt >= cutoff)
                .Concat(clusters.Select(c => new Record(c.Tokens.ToList(), DateTimeOffset.UtcNow)))
                .ToList();

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            await File.WriteAllTextAsync(Path,
                JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }), ct);

            logger.LogInformation("Recorded {New} stories; {Total} in state.", clusters.Count, updated.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not write state file; tomorrow may repeat today's brief.");
        }
    }

    private sealed record Record(List<string> Tokens, DateTimeOffset SentAt);
}