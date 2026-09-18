using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace MorningBrief;

public static class ServiceConfiguration
{
    public static void Register(IServiceCollection services)
    {
        var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("GEMINI_API_KEY not configured.");

        services.AddHttpClient(nameof(FeedReader), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MorningBrief/1.0 (personal news digest)");
        }).RemoveAllLoggers();

        services.AddHttpClient(nameof(BriefWriter), c =>
        {
            c.DefaultRequestHeaders.Add("x-goog-api-key", geminiKey);
        })
        .RemoveAllLoggers()
        .AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = 4;
            o.Retry.Delay = TimeSpan.FromSeconds(4);
            o.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
        });

        services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
        }).RemoveAllLoggers();

        services.AddSingleton<IFeedReader, FeedReader>();
        services.AddSingleton<IStoryClusterer, StoryClusterer>();
        services.AddSingleton<IBriefWriter, BriefWriter>();
    }
}