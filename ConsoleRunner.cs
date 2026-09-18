using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MorningBrief;

public static class ConsoleRunner
{
    public static async Task<int> RunAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Information));
        ServiceConfiguration.Register(services);
        services.AddSingleton<ISentStoryStore, FileSentStoryStore>();
        services.AddSingleton<Notifier>();
        services.AddSingleton<MorningBriefFunction>();

        await using var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<Notifier>();

        try
        {
            await provider.GetRequiredService<MorningBriefFunction>().Execute(cts.Token);
            return 0;
        }
        catch (Exception ex)
        {
            provider.GetRequiredService<ILogger<MorningBriefFunction>>().LogError(ex, "Run failed.");
            await notifier.SendAsync($"⚠️ Morning brief failed: {ex.GetType().Name} — {ex.Message}", CancellationToken.None);
            return 1;
        }
    }
}