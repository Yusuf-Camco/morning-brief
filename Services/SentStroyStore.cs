using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public sealed class SentStoryStore(TableServiceClient tableService, ILogger<SentStoryStore> logger) : ISentStoryStore
{
    private const string TableName = "sentstories";

    public async Task<IReadOnlyList<HashSet<string>>> GetRecentAsync(DateTimeOffset since, CancellationToken ct)
    {
        try
        {
            var table = tableService.GetTableClient(TableName);
            await table.CreateIfNotExistsAsync(cancellationToken: ct);

            var results = new List<HashSet<string>>();

            await foreach (var entity in table.QueryAsync<SentStory>(
                e => e.PartitionKey == "sent" && e.SentAt >= since, cancellationToken: ct))
            {
                results.Add(entity.Tokens.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet());
            }

            logger.LogInformation("Loaded {Count} previously sent stories.", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not read sent-story history; proceeding without deduplication.");
            return [];
        }
    }

    public async Task RecordAsync(IReadOnlyList<StoryCluster> clusters, CancellationToken ct)
    {
        try
        {
            var table = tableService.GetTableClient(TableName);
            await table.CreateIfNotExistsAsync(cancellationToken: ct);

            foreach (var cluster in clusters)
            {
                await table.UpsertEntityAsync(new SentStory
                {
                    RowKey = Guid.NewGuid().ToString("n"),
                    Tokens = string.Join(' ', StoryClusterer.TokenizeTitle(cluster.Primary.Title)),
                    SentAt = DateTimeOffset.UtcNow
                }, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not record sent stories; tomorrow may repeat today's brief.");
        }
    }
}