using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace MorningBrief;

public sealed class RunNowFunction(MorningBriefFunction brief)
{
    [Function(nameof(RunNowFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "run")] HttpRequestData request,
        CancellationToken ct)
    {
        await brief.Execute(ct);

        var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteStringAsync("Brief run complete.", ct);
        return response;
    }
}