using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;

namespace CpmDemoApp;

internal class WhatsAppChannelBehavior : DefaultChannelBehavior
{

    public static string ChannelId { get; } = "twilio-sms";

    private static readonly HashAlgorithm _algorithm = MD5.Create();

    private static readonly string _titlePattern = @"\*\*(.*?)\*\*";
    private static readonly string _titleReplacement = "*$1*";
    private static readonly string _bulletPointPattern = @"^\s*-\s";
    private static readonly string _bulletPointReplacement = "- ";
    private static readonly string _linkPattern = @"\[([^\]]+)\]\(([^)]+)\)";
    private static readonly string _linkReplacement = "$1: $2";
    private static readonly string _replicatePattern = @"^(###\s+[^\r\n]+)";
    private static readonly string _replicateReplacement = string.Empty;
    private static readonly string _strikethroughPattern = @"~~(.*?)~~";
    private static readonly string _strikethroughReplacement = "~$1~";

    private static string FormatOutboundMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Convert titles to bold
        string result = Regex.Replace(text, _titlePattern, _titleReplacement);

        // Keep bullet points as is
        result = Regex.Replace(result, _bulletPointPattern, _bulletPointReplacement, RegexOptions.Multiline);

        // Remove the square brackets from links
        result = Regex.Replace(result, _linkPattern, _linkReplacement);

        // Replicate "### dwsfs" or "### hotel" with a blank space
        result = Regex.Replace(result, _replicatePattern, _replicateReplacement, RegexOptions.Multiline);

        // Strikethrough: ~~text~~ to ~text~
        result = Regex.Replace(result, _strikethroughPattern, _strikethroughReplacement);

        return result;
    }

    private static List<string> ExtractItems(string text)
    {
        var itemPattern = new Regex(@"(\n[1-3]\.\s\*.*?)(?=(\n[1-3]\.|$))", RegexOptions.Singleline);
        var itemMatches = itemPattern.Matches(text);

        var itemsList = new List<string>();
        int lastIndex = 0;

        foreach (Match itemMatch in itemMatches)
        {
            if (itemMatch.Index > lastIndex)
            {
                string partBeforeMatch = text.Substring(lastIndex, itemMatch.Index - lastIndex).Trim();
                if (!string.IsNullOrWhiteSpace(partBeforeMatch))
                {
                    itemsList.Add(partBeforeMatch);
                }
            }

            itemsList.Add(itemMatch.Value.Trim());
            lastIndex = itemMatch.Index + itemMatch.Length;
        }

        if (lastIndex < text.Length)
        {
            string partAfterLastMatch = text.Substring(lastIndex).Trim();
            if (!string.IsNullOrWhiteSpace(partAfterLastMatch))
            {
                itemsList.Add(partAfterLastMatch);
            }
        }

        return itemsList;
    }

    private static List<string> SplitString(string text, int maxLength)
    {
        List<string> parts = new();
        int textLength = text.Length;
        int startIndex = 0;
        char charDot = '.', charSpace = ' ';

        while (startIndex < textLength)
        {
            //int length = Math.Min(maxLength, textLength - startIndex);
            int length = textLength - startIndex;
            if (length > maxLength)
                length = maxLength;
            int splitIndex = startIndex + length;

            if (splitIndex < textLength)
            {
                int lastPeriodIndex = text.LastIndexOf(charDot, splitIndex, length);
                if (lastPeriodIndex > startIndex)
                {
                    splitIndex = lastPeriodIndex + 1; // Include the period
                }
                else
                {
                    int lastSpaceIndex = text.LastIndexOf(charSpace, splitIndex, length);
                    if (lastSpaceIndex > startIndex)
                    {
                        splitIndex = lastSpaceIndex;
                    }
                }
            }

            parts.Add(text[startIndex..splitIndex].Trim());
            startIndex = splitIndex;

            // Skip the space if it exists to prevent adding an extra space at the beginning of the next part
            while (startIndex < textLength && char.IsWhiteSpace(text[startIndex]))
            {
                startIndex++;
            }
        }

        return parts;
    }

    public override async Task<ResourceResponse> SendReplyOutAsync(ITurnContext turnContext, string replyText, CancellationToken cancellationToken)
    {
        int maxLength = 50000;
        string text = FormatOutboundMessage(replyText);

        if (text.Length > maxLength)
        {
            List<string> replyParts = ExtractItems(text);
            ResourceResponse response = new();

            if (replyParts != null && replyParts.Count > 1)
            {
                List<string> processedParts = new();

                foreach (var part in replyParts)
                {
                    if (part.Length > maxLength)
                    {
                        processedParts.AddRange(SplitString(part, maxLength));
                    }
                    else
                    {
                        processedParts.Add(part);
                    }
                }

                foreach (var part in processedParts)
                {
                    response = (await turnContext.SendActivitiesAsync(new Activity[] { MessageFactory.Text(part) }, cancellationToken)).First();
                    await Task.Delay(1000, cancellationToken);
                }
            }
            else
            {
                List<string> fallbackReplyParts = SplitString(text, maxLength);

                foreach (var part in fallbackReplyParts)
                {
                    response = (await turnContext.SendActivitiesAsync(new Activity[] { MessageFactory.Text(part) }, cancellationToken)).First();
                }
            }
            return response;
        }
        else
        {
            return (await turnContext.SendActivitiesAsync(new Activity[] { MessageFactory.Text(text) }, cancellationToken)).First();
        }
    }

    public override Dictionary<string, object?> GetWorthPersistingProperties(ITurnContext turnContext)
    {
        string phoneNumber = turnContext.Activity.Conversation.Id[9..]; //formato: whatsapp:+393331234567
        return new()
        {
            { "mobile", phoneNumber[..4] + "*" + phoneNumber[^3..] } //esempio: +39*567
        };
    }

    public override string GetSafeForPersistenceConversationId(ITurnContext turnContext)
    {
        return Convert.ToHexString(_algorithm.ComputeHash(Encoding.UTF8.GetBytes(turnContext.Activity.Conversation.Id)));
    }
}

internal static class MobileIntlPrefixes
{
    private static readonly Dictionary<string, string> _prefixToLanguage = new()
    {
        { "+1", "English" },
        { "+7", "Russian" },
        { "+20", "Arabic" },
        { "+27", "English" },
        { "+30", "Greek" },
        { "+31", "Dutch" },
        { "+32", "Dutch" },
        { "+33", "French" },
        { "+34", "Spanish" },
        { "+36", "Hungarian" },
        { "+39", "Italian" },
        { "+40", "Romanian" },
        { "+41", "German" },
        { "+44", "English" },
        { "+45", "Danish" },
        { "+46", "Swedish" },
        { "+47", "Norwegian" },
        { "+48", "Polish" },
        { "+49", "German" },
        { "+51", "Spanish" },
        { "+52", "Spanish" },
        { "+53", "Spanish" },
        { "+54", "Spanish" },
        { "+55", "Portuguese" },
        { "+56", "Spanish" },
        { "+57", "Spanish" },
        { "+58", "Spanish" },
        { "+60", "English" },
        { "+61", "English" },
        { "+62", "Indonesian" },
        { "+63", "Filipino" },
        { "+64", "English" },
        { "+65", "Mandarin" },
        { "+66", "Thai" },
        { "+81", "Japanese" },
        { "+82", "Korean" },
        { "+84", "Vietnamese" },
        { "+86", "Chinese" },
        { "+90", "Turkish" },
        { "+91", "Hindi" },
        { "+92", "Urdu" },
        { "+93", "Pashto" },
        { "+94", "Sinhala" },
        { "+95", "Burmese" },
        { "+98", "Persian" },
        { "+212", "Arabic" },
        { "+213", "Arabic" },
        { "+216", "Arabic" },
        { "+218", "Arabic" },
        { "+220", "English" },
        { "+221", "French" },
        { "+222", "Arabic" },
        { "+223", "French" },
        { "+234", "English" },
        { "+250", "Kinyarwanda" },
        { "+251", "Amharic" },
        { "+254", "Swahili" },
        { "+255", "Swahili" },
        { "+256", "Swahili" },
        { "+260", "English" },
        { "+263", "English" }
    };

    //TODO - estendere con le altre Locale
    private static readonly Dictionary<string, string> _prefixToLocale = new()
    {
        { "+1", "en-US" },
        { "+7", "ru-RU" },
        { "+33", "fr-FR" },
        { "+34", "es-ES" },
        { "+39", "it-IT" },
        { "+44", "en-GB" },
        { "+49", "de-DE" },
        { "+82", "ko-KR" },
        { "+351", "pt-PT" }
    };

    public static string GetLanguageFromPhonePrefix(string phoneNumber)
    {
        return LookupPhonePrefix(_prefixToLanguage, phoneNumber, "English");
    }

    public static string GetLocaleFromPhonePrefix(string phoneNumber)
    {
        return LookupPhonePrefix(_prefixToLocale, phoneNumber, "en-US");
    }

    private static string LookupPhonePrefix(Dictionary<string, string> dictionary, string phoneNumber, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return defaultValue;

        string prefix = phoneNumber[9..13]; // whatsapp:+251000.... --> +251
        if (dictionary.TryGetValue(prefix, out string? value))
            return value;

        prefix = prefix[..3]; // +251 --> +25
        if (dictionary.TryGetValue(prefix, out value))
            return value;

        prefix = prefix[..2]; // +25 --> +2
        if (dictionary.TryGetValue(prefix, out value))
            return value;

        return defaultValue;
    }
}
