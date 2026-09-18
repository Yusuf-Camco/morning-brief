using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public sealed class Notifier(IHttpClientFactory httpClientFactory, ILogger<Notifier> logger)
{
    public async Task SendAsync(string text, CancellationToken ct)
    {
        try
        {
            var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "";
            var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? "";
            if (token.Length == 0 || chatId.Length == 0) return;

            var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync(
                $"https://api.telegram.org/bot{token}/sendMessage",
                new { chat_id = chatId, text, parse_mode = "HTML", disable_web_page_preview = true },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notifier failed.");
        }
    }
}