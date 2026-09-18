namespace MorningBrief;

public sealed record NewsItem(string Title, string Link, string Source, DateTimeOffset Published);