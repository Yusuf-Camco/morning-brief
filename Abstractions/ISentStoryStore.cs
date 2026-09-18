namespace MorningBrief;

public interface ISentStoryStore
{
    Task<IReadOnlyList<HashSet<string>>> GetRecentAsync(DateTimeOffset since, CancellationToken ct);
    Task RecordAsync(IReadOnlyList<StoryCluster> clusters, CancellationToken ct);
}