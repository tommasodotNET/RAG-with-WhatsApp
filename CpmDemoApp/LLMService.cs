using Azure;
using Azure.AI.OpenAI;
using CpmDemoApp.Models;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;

namespace CpmDemoApp;

public class LLMService : ActivityHandler
{
    private static AzureOpenAIClient _azureOpenAIClient;
    private static string _deploymentName;
    private static IServiceProvider _serviceProvider;
    private static string SystemPrompt => "You are Contoso Electronics AI customer service assistant who helps resolve queries of customers." +
            "When a customer sends you the first message, you greet them and ask them if they need help with their calculator." +
            "You ask them the error code on the screen and use the below context to help them resolve the issue." +
            "You maintain a professional and friendly tone." +
            "If you do not find answer in the context below, you do not search the web. Instead you say 'I do not know how to fix this one. Please call customer service. Thank you'" +
            "Context for answering questions" +
            "Code: 1000\r\nErrorName: Math ERROR\r\nCause: Calculation or input exceeds range or involves an illegal operation.\r\nAction: Adjust input values and press “Clear” to retry.\r\n\r\n" +
            "Code: 1001\r\nErrorName: Stack ERROR\r\nCause: Stack capacity exceeded.\r\nAction: Simplify your expression and press “Enter” to try again.\r\n\r\n" +
            "Code: 1002\r\nErrorName: Syntax ERROR\r\nCause: Calculation format issue.\r\nAction: Correct the format, then press “Clear” and re-enter the calculation.\r\n\r\n" +
            "Code: 1003\r\nErrorName: Dimension ERROR (MATRIX and VECTOR)\r\nCause: Matrix/vector dimensions not specified or incompatible.\r\nAction: Verify dimensions, then press “Clear” and re-enter the matrix/vector.\r\n\r\n" +
            "Code: 1004\r\nErrorName: Variable ERROR (SOLVE)\r\nCause: Missing or incorrect solution variable.\r\nAction: Include the variable in your equation, then press “Solve” again.\r\n\r\n" +
            "Code: 1005\r\nErrorName: Can't Solve Error (SOLVE)\r\nCause: Solution could not be obtained.\r\nAction: Check your equation for errors, then adjust your input and press “Solve” again.";

    public LLMService(IOptions<OpenAIClientOptions> AIOptions, IServiceProvider serviceProvider)
    {
        _deploymentName = AIOptions.Value.DeploymentName;
        _azureOpenAIClient = new AzureOpenAIClient(new Uri(AIOptions.Value.Endpoint), new ApiKeyCredential(AIOptions.Value.Key));
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        string channelId = turnContext.Activity.ChannelId;

        //recuperiamo il behavior specifico per il channel, oppure il DefaultChannelBehavior
        var channelBehavior = new WhatsAppChannelBehavior();
        Debug.Assert(channelBehavior != null);

        string conversationId = channelBehavior.GetSafeForPersistenceConversationId(turnContext);

        var response = await GenerateAIResponseAsync(turnContext.Activity.Text);

        _ = await channelBehavior.SendReplyOutAsync(turnContext, response!, cancellationToken).ConfigureAwait(false);
    }

    public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        await base.OnMessageActivityAsync(turnContext as ITurnContext<IMessageActivity>, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> GenerateAIResponseAsync(string request)
    {
        var chatMessages = new List<ChatMessage> { new SystemChatMessage(SystemPrompt) };
        chatMessages.Add(request);
        ChatCompletion response = await _azureOpenAIClient.GetChatClient(_deploymentName).CompleteChatAsync(chatMessages);
        return response?.Content.FirstOrDefault()?.Text;
    }
}
