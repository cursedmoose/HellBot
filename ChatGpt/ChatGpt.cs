using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using TwitchLib.Client.Models;
using System.Globalization;
using OpenAI.Models;
using static TwitchBot.Config.ChatGptConfig;
using static TwitchBot.Config.DiscordConfig;

namespace TwitchBot.ChatGpt
{
    public class ChatGpt
    {
        readonly OpenAIClient openAI;
        static readonly ChatGptUsage usage = new();
        readonly bool isEnabled;
        readonly Logger log = new("ChatGpt");

        public ChatGpt(bool enabled = true)
        {
            openAI = new(openAIAuthentication: new(API_KEY, ORGANIZATION_ID));
            isEnabled = enabled;
        }

        public void GetUsage()
        {
            log.Info($"[Usage][Prompts] Total: {ChatGptUsage.prompts.tokens_used}");
            log.Info($"[Usage][Prompts]   Avg: {ChatGptUsage.prompts.tokens_used / Math.Max(1, ChatGptUsage.prompts.requests_made)}");
            log.Info($"[Usage][Prompts]   Low: {ChatGptUsage.prompts.lowest}");
            log.Info($"[Usage][Prompts]  High: {ChatGptUsage.prompts.highest}");
            log.Info($"[Usage][Completions] Total: {ChatGptUsage.completions.tokens_used}");
            log.Info($"[Usage][Completions]   Avg: {ChatGptUsage.completions.tokens_used / Math.Max(1, ChatGptUsage.completions.requests_made)}");
            log.Info($"[Usage][Completions]   Low: {ChatGptUsage.completions.lowest}");
            log.Info($"[Usage][Completions]  High: {ChatGptUsage.completions.highest}");
            log.Info($"[Usage][Total] Total: {ChatGptUsage.total.tokens_used}");
            log.Info($"[Usage][Total]   Avg: {ChatGptUsage.total.tokens_used / Math.Max(1, ChatGptUsage.total.requests_made)}");
            log.Info($"[Usage][Total]   Low: {ChatGptUsage.total.lowest}");
            log.Info($"[Usage][Total]  High: {ChatGptUsage.total.highest}");
        }

        public static List<Message> ConvertToMessages(List<string> strings)
        {
            var chatPrompts = new List<Message>();
            foreach(string message in strings)
            {
                chatPrompts.Add(new Message(Role.User, message));
            }
            return chatPrompts;
        }

        private async Task<bool> RequestResponses(List<Message> messages)
        {
            try
            {
                var result = await RequestResponseText(messages);
                Server.Instance.Assistant.PlayTts(result);
            }
            catch (Exception e)
            {
                log.Error($"Got error: {e.Message}");
                return false;
            }

            return true;
        }

        private async Task<string> RequestResponseText(List<Message> messages)
        {
            var chatRequest = new ChatRequest(
                model: Model.GPT3_5_Turbo,
                messages: messages,
                temperature: 1.25,
                maxTokens: 75);
            var responseText = "";
            try
            {
                var result = await openAI.ChatEndpoint.GetCompletionAsync(chatRequest);
                responseText = result.FirstChoice;
                usage.recordUsage(result.Usage);
            }
            catch (Exception e)
            {
                log.Error($"Got error: {e.Message}");
            }

            return responseText;
        }

        public async Task<string> GetResponseText(string persona, string chatPrompt)
        {
            if (!isEnabled) { return ""; }

            log.Info($"Asking ChatGpt to respond to {chatPrompt}");

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
                new(Role.User, chatPrompt)
            };

            return await RequestResponseText(chatPrompts);
        }

        public async Task<string> GetResponseText(string persona, List<Message> messages)
        {
            if (!isEnabled) { return ""; }

            log.Info($"Asking ChatGpt to respond to {messages.Count} messages.");

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
            };
            chatPrompts.AddRange(messages);

            return await RequestResponseText(chatPrompts);
        }

        public async Task<bool> GetResponse(string persona, string chatPrompt, int maxResponseLength = 25)
        {
            if (!isEnabled) { return false; }

            log.Info($"Asking ChatGpt to respond to {chatPrompt}");
            string promptTemplate = GeneratePromptFromTemplate(chatPrompt, maxResponseLength);

            log.Info(promptTemplate);

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
                new(Role.User, promptTemplate)
            };

            return await RequestResponses(chatPrompts);
        }

        public async Task<string> GetImage(string imagePrompt, ChatMessage? message = null)
        {
            var results = await openAI.ImagesEndPoint.GenerateImageAsync(imagePrompt, 1, ImageSize.Medium);
            var result = results[0];
            var shortUrl = await Server.Instance.ShortenUrl(result);

            log.Info($"Generated image: {shortUrl}");
            var title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(imagePrompt);
            Server.Instance.discord.PostMessage(Channel.JustMe.IMAGES, $"\"{title}\":");
            Server.Instance.discord.PostMessage(Channel.JustMe.IMAGES, $"{shortUrl}");

            if (message != null) {
                Server.Instance.twitch.RespondTo(message, $"{title}: {shortUrl}");
            }
            var author = message == null ? Server.Instance.Assistant.Name : message.DisplayName;
            var fileName = $"[{author}] {title}";
            Server.Instance.SaveImageAs(result, fileName, ".png");

            return shortUrl;
        }

        private static string GeneratePromptFromTemplate(string chatPrompt, int maxResponseLength)
        {
            return $"{chatPrompt}. limit {maxResponseLength} words";
        }
    }
}
