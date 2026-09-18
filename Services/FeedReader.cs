using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public sealed class FeedReader(IHttpClientFactory httpClientFactory, ILogger<FeedReader> logger) : IFeedReader
{
    public async Task<IReadOnlyList<NewsItem>> FetchAsync(DateTimeOffset since, CancellationToken ct)
    {
        var tasks = FeedSources.All.Select(source => FetchOneAsync(source.Name, source.Url, since, ct));
        var results = await Task.WhenAll(tasks);

        return results.SelectMany(r => r).ToList();
    }

    private async Task<IReadOnlyList<NewsItem>> FetchOneAsync(
        string name, string url, DateTimeOffset since, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(FeedReader));
            await using var stream = await client.GetStreamAsync(url, ct);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Ignore });

            var feed = SyndicationFeed.Load(reader);
            if (feed is null)
            {
                logger.LogWarning("Feed {Source} returned no parseable content.", name);
                return [];
            }

            var items = feed.Items
                .Where(i => i.PublishDate > since || i.LastUpdatedTime > since)
                .Select(i => new NewsItem(
                    Title: i.Title?.Text?.Trim() ?? string.Empty,
                    Link: i.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty,
                    Source: name,
                    Published: i.PublishDate == default ? i.LastUpdatedTime : i.PublishDate))
                .Where(i => i.Title.Length > 0 && i.Link.Length > 0)
                .ToList();

            logger.LogInformation("Feed {Source}: {Count} items in window.", name, items.Count);
            return items;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Feed {Source} failed.", name);
            return [];
        }
    }
}