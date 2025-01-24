using Azure.AI.OpenAI;
using Azure;
using Azure.Messaging.EventGrid;
using CpmDemoApp.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using OpenAI.Chat;
using System.Text.Json;
using System.Globalization;
using System.Net;
using Azure.Communication.Messages;
using Microsoft.Extensions.Options;
using System.ClientModel;
using System.Text;

namespace CpmDemoApp;

public class ACSAdapter : BotAdapter, IBotFrameworkHttpAdapter
{
    private static bool _clientsInitialized;
    private static NotificationMessagesClient _notificationMessagesClient;
    private static Guid _channelRegistrationId;

    public ACSAdapter(
            IOptions<NotificationMessagesClientOptions> notificationOptions)
    {
        if (!_clientsInitialized)
        {
            _channelRegistrationId = Guid.Parse(notificationOptions.Value.ChannelRegistrationId);
            _notificationMessagesClient = new NotificationMessagesClient(notificationOptions.Value.ConnectionString);
            _clientsInitialized = true;
        }
    }

    public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task ProcessAsync(string jsonContent, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default)
    {
        var _jsonOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
        var eventGridEvent = JsonSerializer.Deserialize<EventGridEvent[]>(jsonContent, _jsonOptions).First();

        var activity = ACSHelper.EventGridEventToActivity(eventGridEvent, _channelRegistrationId);
        using TurnContext context = new TurnContext(this, activity);
        context.TurnState.Add("httpStatus", HttpStatusCode.OK.ToString("D"));
        await RunPipelineAsync(context, bot.OnTurnAsync, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    public Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Activity[] activities, CancellationToken cancellationToken)
    {
        List<ResourceResponse> responses = new List<ResourceResponse>();
        foreach (Activity activity in activities)
        {
            var recipientList = new List<string> { activity.From.Id };
            var textContent = new TextNotificationContent(_channelRegistrationId, recipientList, activity.Text);
            await _notificationMessagesClient.SendAsync(textContent);
            ResourceResponse item = new ResourceResponse
            {
                Id = activity.From.Id
            };
            responses.Add(item);
        }

        return responses.ToArray();
    }

    public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, Activity activity, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}


public static class ACSHelper
{
    public static Activity EventGridEventToActivity(EventGridEvent eventGridEvent, Guid channelRegistrationId)
    {
        var _jsonOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
        var messageData = JsonSerializer.Deserialize<AdvancedMessageReceivedEventData>(eventGridEvent.Data.ToString(), _jsonOptions);
        
        var activity = new Activity()
        {
            Id = eventGridEvent.Id,
            Timestamp = eventGridEvent.EventTime,
            Type = ActivityTypes.Message,
            ChannelId = channelRegistrationId.ToString(),
            ServiceUrl = eventGridEvent.Topic,
            From = new ChannelAccount() { Id = messageData.From },
            Conversation = new ConversationAccount() { Id = messageData.From },
            Recipient = new ChannelAccount() { Id = messageData.To },
            Text = messageData.Content,
            Value = messageData.Content,
        };
        return activity;
    }
}