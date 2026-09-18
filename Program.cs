using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using MorningBrief;
using Microsoft.Extensions.Options;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.AddHttpClient(nameof(FeedReader), client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MorningBrief/1.0 (personal news digest)");
}).RemoveAllLoggers();
var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
    ?? throw new InvalidOperationException("GEMINI_API_KEY not configured.");

builder.Services.AddHttpClient(nameof(BriefWriter), client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("x-goog-api-key", geminiKey);
})
.RemoveAllLoggers()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 4;
    options.Retry.Delay = TimeSpan.FromSeconds(4);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddSingleton<IBriefWriter, BriefWriter>();
builder.Services.AddSingleton<IFeedReader, FeedReader>();
builder.Services.AddSingleton<IStoryClusterer, StoryClusterer>();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}
builder.Services.AddHttpClient(Options.DefaultName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).RemoveAllLoggers();
builder.Build().Run();
