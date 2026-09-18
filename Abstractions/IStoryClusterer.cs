namespace MorningBrief;

public interface IStoryClusterer
{
    IReadOnlyList<StoryCluster> Cluster(IReadOnlyList<NewsItem> items);
}