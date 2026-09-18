using System.Net.Http.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public sealed class MorningBrief(IHttpClientFactory httpClientFactory, ILogger<MorningBrief> logger)
{
    [Function(nameof(MorningBrief))]
    public async Task Run(
        [TimerTrigger("0 0 6 * * *", RunOnStartup = true)] TimerInfo timer,
        CancellationToken ct)
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
            ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN not configured.");
        var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
            ?? throw new InvalidOperationException("TELEGRAM_CHAT_ID not configured.");

        var client = httpClientFactory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text = "Morning brief pipeline is alive." },
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