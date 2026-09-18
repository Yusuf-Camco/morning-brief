using System.Net.Http.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public sealed class MorningBriefFunction(
    IFeedReader feedReader,
    IStoryClusterer clusterer,
    IHttpClientFactory httpClientFactory,
    ILogger<MorningBriefFunction> logger)
{
    [Function(nameof(MorningBriefFunction))]
    public async Task Run(
        [TimerTrigger("0 0 6 * * *", RunOnStartup = true)] TimerInfo timer,
        CancellationToken ct)
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
            ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN not configured.");
        var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
            ?? throw new InvalidOperationException("TELEGRAM_CHAT_ID not configured.");

        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var items = await feedReader.FetchAsync(since, ct);

        logger.LogInformation(
            "Fetched {Count} items across {Sources} sources.",
            items.Count,
            items.Select(i => i.Source).Distinct().Count());

        if (items.Count == 0)
        {
            logger.LogWarning("No items fetched; nothing to send.");
            return;
        }

        var clusters = clusterer.Cluster(items)
            .Where(c => c.SourceCount >= 2)
            .OrderByDescending(c => c.SourceCount)
            .ThenByDescending(c => c.Primary.Published)
            .Take(8)
            .ToList();

        logger.LogInformation("Clustered {Items} items into {Clusters} stories.", items.Count, clusters.Count);

        var text = string.Join("\n\n", clusters.Select((c, n) =>
            $"{n + 1}. {c.Primary.Title}\n" +
            $"{c.SourceCount} sources · {string.Join(", ", c.Sources.Select(s => s.Source).Distinct())}\n" +
            $"{c.Primary.Link}"));

        var client = httpClientFactory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text, disable_web_page_preview = true },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Telegram send failed: {Status} {Body}",
                response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));
            return;
        }

        logger.LogInformation("Brief delivered.");
    }
}