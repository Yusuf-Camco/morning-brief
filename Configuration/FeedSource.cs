namespace MorningBrief;

public static class FeedSources
{
    public static readonly IReadOnlyList<(string Name, string Url)> All =
    [
        ("Sky News World",  "https://feeds.skynews.com/feeds/rss/world.xml"),
        ("BBC World",       "https://feeds.bbci.co.uk/news/world/rss.xml"),
        ("Al Jazeera",      "https://www.aljazeera.com/xml/rss/all.xml"),
        ("CBS World",       "https://www.cbsnews.com/latest/rss/world"),
        ("Guardian World",  "https://www.theguardian.com/world/rss"),
        ("NPR World",       "https://feeds.npr.org/1004/rss.xml"),
        ("NYT World",       "https://rss.nytimes.com/services/xml/rss/nyt/World.xml"),
        ("France24",        "https://www.france24.com/en/rss"),
        ("Premium Times",   "https://www.premiumtimesng.com/feed"),
        ("AllAfrica",       "https://allafrica.com/tools/headlines/rdf/latest/headlines.rdf"),
    ];
}