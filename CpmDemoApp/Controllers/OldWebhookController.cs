﻿using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using CpmDemoApp.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CpmDemoApp.Controllers
{
    [ApiController]
    [Route("oldWebhook")]
    //[Consumes("application/json")]
    public class OldWebhookController : Controller
    {
        [HttpOptions]
        public async Task<IActionResult> Options()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                // Retrieve the validation header fields
                var webhookRequestOrigin = HttpContext.Request.Headers["WebHook-Request-Origin"].FirstOrDefault();
                var webhookRequestCallback = HttpContext.Request.Headers["WebHook-Request-Callback"];
                var webhookRequestRate = HttpContext.Request.Headers["WebHook-Request-Rate"];
                // Respond with the appropriate origin and allowed rate to
                // confirm acceptance of incoming notications
                HttpContext.Response.Headers.Add("WebHook-Allowed-Rate", "*");
                HttpContext.Response.Headers.Add("WebHook-Allowed-Origin", webhookRequestOrigin);
            }
            return Ok();
        }

        [HttpPost]
        public JObject Post([FromBody] object request)
        {
            var eventGridEvents = JsonSerializer.Deserialize<EventGridEvent[]>(request.ToString());
            EventGridEvent eventGridEvent = eventGridEvents.FirstOrDefault();

            if (eventGridEvent == null) return new JObject(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            var data = eventGridEvent.Data.ToString();

            if (string.Equals(eventGridEvent.EventType, "Microsoft.EventGrid.SubscriptionValidationEvent", StringComparison.OrdinalIgnoreCase))
            {
                if (eventGridEvent.Data != null)
                {
                    var eventData = JsonSerializer.Deserialize<SubscriptionValidationEventData>(data);
                    var responseData = new SubscriptionValidationResponse
                    {
                        ValidationResponse = eventData.ValidationCode
                    };

                    if (responseData.ValidationResponse != null)
                    {
                        return JObject.FromObject(responseData);
                    }
                }
            }
            else
            {
                if (data == null) return new JObject(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
                var eventData = JsonSerializer.Deserialize<ExperimentalEventData>(data);
                return JObject.FromObject(eventData);
            }

            return new JObject(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
    
}
