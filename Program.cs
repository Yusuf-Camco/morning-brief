using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MorningBrief;

if (args.Contains("--run-once"))
    return await ConsoleRunner.RunAsync();

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

ServiceConfiguration.Register(builder.Services);

builder.Services.AddSingleton(new Azure.Data.Tables.TableServiceClient(
    Environment.GetEnvironmentVariable("AzureWebJobsStorage")!));
builder.Services.AddSingleton<ISentStoryStore, SentStoryStore>();
builder.Services.AddSingleton<MorningBriefFunction>();

builder.Build().Run();
return 0;