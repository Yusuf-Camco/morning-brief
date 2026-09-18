namespace MorningBrief;

public sealed class StoryClusterer : IStoryClusterer
{
    private const double SimilarityThreshold = 0.30;

    private static readonly HashSet<string> Stopwords =
    [
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with",
        "as", "by", "from", "is", "are", "was", "were", "be", "been", "it", "its", "this",
        "that", "these", "those", "after", "over", "says", "said", "new", "amid", "into"
    ];

    public IReadOnlyList<StoryCluster> Cluster(IReadOnlyList<NewsItem> items)
    {
        var tokenized = items
            .Select(i => (Item: i, Tokens: Tokenize(i.Title)))
            .Where(x => x.Tokens.Count >= 3)
            .ToList();

        var assigned = new bool[tokenized.Count];
        var clusters = new List<StoryCluster>();

        for (var i = 0; i < tokenized.Count; i++)
        {
            if (assigned[i]) continue;

            var members = new List<NewsItem> { tokenized[i].Item };
            assigned[i] = true;

            for (var j = i + 1; j < tokenized.Count; j++)
            {
                if (assigned[j]) continue;
                if (Jaccard(tokenized[i].Tokens, tokenized[j].Tokens) < SimilarityThreshold) continue;

                members.Add(tokenized[j].Item);
                assigned[j] = true;
            }

            clusters.Add(new StoryCluster(members[0], members));
        }

        return clusters;
    }

    private static HashSet<string> Tokenize(string title)
    {
        return [.. new string([.. title.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(t => t.Length > 1 && !Stopwords.Contains(t))];
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        var intersection = a.Count <= b.Count ? a.Count(b.Contains) : b.Count(a.Contains);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }
}