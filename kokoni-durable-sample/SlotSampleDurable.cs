using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace kokonidurablesample
{
    public static class SlotSampleDurable
    {
        private static HttpClient client = new HttpClient();

        [FunctionName("SlotSampleDurable")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,TraceWriter log)
        {
            var outputs = new List<string>();
            DateTime endTime = context.CurrentUtcDateTime.AddMinutes(10);

            while (context.CurrentUtcDateTime < endTime)
            {
                if (!context.IsReplaying)
                {
                    log.Info($"This event occurs {context.CurrentUtcDateTime}.");
                }

                outputs.Add(await context.CallActivityWithRetryAsync<string>(
                    "SlotSampleDurable_Slack",
                    new RetryOptions(new System.TimeSpan(5000), 10),
                    "Tokyo : " + context.CurrentUtcDateTime.ToString()));

                var nextCheck = context.CurrentUtcDateTime.AddSeconds(30);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            }

            return outputs;
        }

        [FunctionName("SlotSampleDurable_Slack")]
        public static async Task<string> SayHello([ActivityTrigger] string text, TraceWriter log)
        {
            log.Info($"Saying hello to {text}.");

            var json = JsonConvert.SerializeObject(new Message()
            {
                Text = text,
                Icon = ":sunglasses:",
                Username = "テストユーザV1"
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("{SlackIncomingWebHookURL}", content);

            return $"Hello {text}!";
        }

        [FunctionName("SlotSampleDurable_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            TraceWriter log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("SlotSampleDurable", null);

            log.Info($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }

    public class Message
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
        public string Icon { get; set; } = ":cat:";
    }
}