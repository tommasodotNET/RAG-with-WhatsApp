using System.Text;
using Microsoft.AspNetCore.Mvc;
using CpmDemoApp.Models;
using Microsoft.Extensions.Options;
using CpmDemoApp;
using Microsoft.Bot.Builder;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Messaging.EventGrid;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json;

namespace viewer.Controllers
{
    [Route("webhook")]
    public class WebhookController : Controller
    {
        private static ACSAdapter _adapter;
        private static IBot _bot;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private bool EventTypeSubcriptionValidation
            => HttpContext.Request.Headers["aeg-event-type"].FirstOrDefault() ==
               "SubscriptionValidation";

        private bool EventTypeNotification
            => HttpContext.Request.Headers["aeg-event-type"].FirstOrDefault() ==
                "Notification";

        public WebhookController(
            ACSAdapter adapter,
            [FromServices] IBot bot)
        {
            _adapter = adapter;
            _bot = bot;
        }

        [HttpOptions]
        public async Task<IActionResult> Options()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var webhookRequestOrigin = HttpContext.Request.Headers["WebHook-Request-Origin"].FirstOrDefault();
                var webhookRequestCallback = HttpContext.Request.Headers["WebHook-Request-Callback"];
                var webhookRequestRate = HttpContext.Request.Headers["WebHook-Request-Rate"];
                HttpContext.Response.Headers.Add("WebHook-Allowed-Rate", "*");
                HttpContext.Response.Headers.Add("WebHook-Allowed-Origin", webhookRequestOrigin);
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var jsonContent = await reader.ReadToEndAsync();

                // Check the event type.
                // Return the validation code if it's a subscription validation request. 
                if (EventTypeSubcriptionValidation)
                {
                    return await HandleValidation(jsonContent);
                }
                else if (EventTypeNotification)
                {
                    await _adapter.ProcessAsync(jsonContent, Response, _bot);
                    
                    return Ok();
                }

                return BadRequest();
            }
        }

        private async Task<JsonResult> HandleValidation(string jsonContent)
        {
            var eventGridEvent = JsonSerializer.Deserialize<EventGridEvent[]>(jsonContent, _jsonOptions).First();
            var eventData = JsonSerializer.Deserialize<SubscriptionValidationEventData>(eventGridEvent.Data.ToString(), _jsonOptions);
            var responseData = new SubscriptionValidationResponse
            {
                ValidationResponse = eventData.ValidationCode
            };
            return new JsonResult(responseData);
        }
    }
}