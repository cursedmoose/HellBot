using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using TwitchLib.Client.Models;
using System.Globalization;
using OpenAI.Models;
using static TwitchBot.Config.ChatGptConfig;
using static TwitchBot.Config.DiscordConfig;
using Message = OpenAI.Chat.Message;

namespace TwitchBot.ChatGpt
{
    public record ChatGptOptions(
        double? Temperature,
        double? FrequencyPenalty,
        double? PresencePenalty
        )
    {
        public static readonly ChatGptOptions Default = new(1.25, 0, 0);
    }
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

        private async Task<string> RequestResponses(List<Message> messages, ChatGptOptions? options = null)
        {
            try
            {
                var result = await RequestResponseText(messages, options);
                Server.Instance.Assistant.PlayTts(result);
                return result;
            }
            catch (Exception e)
            {
                log.Error($"Got error: {e.Message}");
                return "";
            }
        }

        private async Task<string> RequestResponseText(List<Message> messages, ChatGptOptions? options = null)
        {
            ChatGptOptions api_params = options == null ? ChatGptOptions.Default : options;
            var chatRequest = new ChatRequest(
                model: Model.GPT4,
                messages: messages,
                temperature: api_params.Temperature,
                presencePenalty: api_params.PresencePenalty,
                frequencyPenalty: api_params.FrequencyPenalty,
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

        public async Task<string> GetResponseText(string persona, string chatPrompt, ChatGptOptions? options = null)
        {
            if (!isEnabled) { return ""; }

            log.Info($"Asking ChatGpt to respond to {chatPrompt}");

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
                new(Role.User, chatPrompt)
            };

            return await RequestResponseText(chatPrompts, options);
        }

        public async Task<string> GetResponseText(string persona, List<Message> messages, ChatGptOptions? options = null)
        {
            if (!isEnabled) { return ""; }

            log.Info($"Asking ChatGpt to respond to {messages.Count} messages.");

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
            };
            chatPrompts.AddRange(messages);

            return await RequestResponseText(chatPrompts, options);
        }

        public async Task<string> GetResponse(string persona, string chatPrompt, ChatGptOptions? options = null)
        {
            if (!isEnabled) { return ""; }

            log.Info($"Asking ChatGpt to respond to {chatPrompt}");
            string promptTemplate = GeneratePromptFromTemplate(chatPrompt, 25);

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
            log.Info($"Generating image: {imagePrompt}");
            var results = await openAI.ImagesEndPoint.GenerateImageAsync(
                new ImageGenerationRequest(
                    prompt: imagePrompt,
                    model: Model.DallE_3
                ));

            var result = results[0];
            var shortUrl = await Server.Instance.ShortenUrl(result);

            log.Info($"Generated image: {shortUrl}");
            var title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(imagePrompt);
            Server.Instance.discord.PostMessage(Channel.JustMe.IMAGES, $"\"{title}\":");
            Server.Instance.discord.PostMessage(Channel.JustMe.IMAGES, $"{shortUrl}");

            if (message != null) {
                Server.Instance.twitch.RespondTo(message, $"{title}: {shortUrl}");
            }

            return shortUrl;
        }

        private static string GeneratePromptFromTemplate(string chatPrompt, int maxResponseLength)
        {
            return $"{chatPrompt}. limit {maxResponseLength} words";
        }
    }
}
