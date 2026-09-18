namespace MorningBrief;

public interface IFeedReader
{
    Task<IReadOnlyList<NewsItem>> FetchAsync(DateTimeOffset since, CancellationToken ct);
}