using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public static class MyDurableApp
    {
        [FunctionName(nameof(MyOrchestrator))]
        public static async Task<List<string>> MyOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));

            return outputs;
        }

        [FunctionName(nameof(SayHello))]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName(nameof(HttpStart))]
        public static async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync(nameof(MyOrchestrator), null);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return CreateCustomCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(GetStatus))]
        public static async Task<IActionResult> GetStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "statuses/{instanceId}")]
                HttpRequestMessage req,
            string instanceId,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {
            var orchestrator = await client.GetStatusAsync(instanceId);
            var status = orchestrator.RuntimeStatus.ToString();
            var isRunning = 
                orchestrator.RuntimeStatus == OrchestrationRuntimeStatus.Pending
                || orchestrator.RuntimeStatus == OrchestrationRuntimeStatus.Running;
            
            return isRunning ?
                (IActionResult)new AcceptedResult(GetStatusUrl(instanceId), status) :
                (IActionResult)new OkObjectResult(status);
        }

        private static IActionResult CreateCustomCheckStatusResponse(
            HttpRequestMessage requestMessage,
            string instanceId)
        {
            var statusUrl = GetStatusUrl(instanceId);

            return new AcceptedResult(statusUrl, 
                new
                {
                    id = instanceId,
                    statusQueryGetUri = statusUrl
                });
        }

        private static string GetStatusUrl(string instanceId)
        {
            return $"/api/statuses/{instanceId}";
        }

    }
}