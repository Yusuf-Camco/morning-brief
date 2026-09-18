namespace MorningBrief;

public interface IBriefWriter
{
    Task<IReadOnlyList<BriefStory>> WriteAsync(IReadOnlyList<StoryCluster> clusters, CancellationToken ct);
}