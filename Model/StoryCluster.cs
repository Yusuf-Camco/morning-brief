namespace MorningBrief;

public sealed record StoryCluster(NewsItem Primary, IReadOnlyList<NewsItem> Sources)
{
    public int SourceCount => Sources.Select(s => s.Source).Distinct().Count();

    public HashSet<string> Tokens =>
        Sources.SelectMany(s => StoryClusterer.TokenizeTitle(s.Title)).ToHashSet();
}