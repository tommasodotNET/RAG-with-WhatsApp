using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace CpmDemoApp;

public interface IChannelBehavior
{
    Task<NextAction> ReactToIncomingMessageAsync(ITurnContext<IMessageActivity> turnContext, string message, CancellationToken cancellationToken);

    Task ReactToIncomingEventAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken);

    Task<ResourceResponse> SendReplyOutAsync(ITurnContext turnContext, string replyText, CancellationToken cancellationToken);

    string GetLocale(ITurnContext turnContext);

    bool RequiresStreaming { get; }

    string GetSafeForPersistenceConversationId(ITurnContext turnContext);

    Dictionary<string, object?> GetWorthPersistingProperties(ITurnContext turnContext);
}

public class DefaultChannelBehavior : IChannelBehavior
{
    public virtual bool RequiresStreaming { get; } // = false;

    public virtual string GetLocale(ITurnContext turnContext)
    {
        return turnContext.Activity.GetLocale();
    }

    public virtual Task<NextAction> ReactToIncomingMessageAsync(ITurnContext<IMessageActivity> turnContext, string message, CancellationToken cancellationToken)
    {
        return Task.FromResult(NextAction.Continue);
    }

    public virtual Task ReactToIncomingEventAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual async Task<ResourceResponse> SendReplyOutAsync(ITurnContext turnContext, string replyText, CancellationToken cancellationToken)
    {
        return await turnContext.SendActivityAsync(MessageFactory.Text(replyText), cancellationToken).ConfigureAwait(false);
    }

    public virtual string GetSafeForPersistenceConversationId(ITurnContext turnContext)
    {
        return turnContext.Activity.Conversation.Id;
    }

    public virtual Dictionary<string, object?> GetWorthPersistingProperties(ITurnContext turnContext)
    {
        return new Dictionary<string, object?>();
    }
}

public enum NextAction
{
    Continue,
    EndOfTurn
}