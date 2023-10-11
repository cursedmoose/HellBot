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
        OpenAIClient openAI;
        static ChatGptUsage usage;
        bool isEnabled;

        public ChatGpt(bool enabled = true)
        {
            openAI = new(openAIAuthentication: new(API_KEY, ORGANIZATION_ID));
            isEnabled = enabled;
            usage = new ChatGptUsage();
        }

        public void getUsage()
        {
            Log($"[Usage][Prompts] Total: {ChatGptUsage.prompts.tokens_used}");
            Log($"[Usage][Prompts]   Avg: {ChatGptUsage.prompts.tokens_used / Math.Max(1, ChatGptUsage.prompts.requests_made)}");
            Log($"[Usage][Prompts]   Low: {ChatGptUsage.prompts.lowest}");
            Log($"[Usage][Prompts]  High: {ChatGptUsage.prompts.highest}");
            Log($"[Usage][Completions] Total: {ChatGptUsage.completions.tokens_used}");
            Log($"[Usage][Completions]   Avg: {ChatGptUsage.completions.tokens_used / Math.Max(1, ChatGptUsage.completions.requests_made)}");
            Log($"[Usage][Completions]   Low: {ChatGptUsage.completions.lowest}");
            Log($"[Usage][Completions]  High: {ChatGptUsage.completions.highest}");
            Log($"[Usage][Total] Total: {ChatGptUsage.total.tokens_used}");
            Log($"[Usage][Total]   Avg: {ChatGptUsage.total.tokens_used / Math.Max(1, ChatGptUsage.total.requests_made)}");
            Log($"[Usage][Total]   Low: {ChatGptUsage.total.lowest}");
            Log($"[Usage][Total]  High: {ChatGptUsage.total.highest}");
        }

        public static List<Message> convertToMessages(List<string> strings)
        {
            var chatPrompts = new List<Message>();
            foreach(string message in strings)
            {
                chatPrompts.Add(new Message(Role.User, message));
            }
            return chatPrompts;
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [ChatGpt] {message}");
        }

        private async Task<bool> requestResponses(List<Message> messages)
        {
            try
            {
                var result = await requestResponseText(messages);
                Server.Instance.Assistant.PlayTts(result);
                // Server.Instance.elevenlabs.playTts(result, Server.Instance.Assistant.Voice);
            }
            catch (Exception e)
            {
                Log($"Got error: {e.Message}");
                return false;
            }

            return true;
        }

        private async Task<string> requestResponseText(List<Message> messages)
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
                Log($"Got error: {e.Message}");
            }

            return responseText;
        }

        public async Task<string> getResponseText(string persona, string chatPrompt)
        {
            if (!isEnabled) { return ""; }

            Log($"Asking ChatGpt to respond to {chatPrompt}");
            // string promptTemplate = generatePromptFromTemplate(chatPrompt, maxResponseLength);

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
                new(Role.User, chatPrompt)
            };

            return await requestResponseText(chatPrompts);
        }

        public async Task<string> getResponseText(string persona, List<Message> messages)
        {
            if (!isEnabled) { return ""; }

            Log($"Asking ChatGpt to respond to {messages.Count} messages.");

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
            };
            chatPrompts.AddRange(messages);

            return await requestResponseText(chatPrompts);
        }

        public async Task<bool> getResponse(string persona, string chatPrompt, int maxResponseLength = 25)
        {
            if (!isEnabled) { return false; }

            Log($"Asking ChatGpt to respond to {chatPrompt}");
            string promptTemplate = generatePromptFromTemplate(chatPrompt, maxResponseLength);

            Log(promptTemplate);

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
                new(Role.User, promptTemplate)
            };

            return await requestResponses(chatPrompts);
        }

        public async Task<string> getImage(string imagePrompt, ChatMessage? message = null)
        {
            var results = await openAI.ImagesEndPoint.GenerateImageAsync(imagePrompt, 1, ImageSize.Medium);
            var result = results.First();
            var shortUrl = await Server.Instance.shortenUrl(result);

            Log($"Generated image: {shortUrl}");
            var title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(imagePrompt);
            Server.Instance.discord.PostMessage(Channel.JustMe.IMAGES, $"\"{title}\":");
            Server.Instance.discord.PostMessage(Channel.JustMe.IMAGES, $"{shortUrl}");

            if (message != null) {
                Server.Instance.twitch.RespondTo(message, $"{title}: {shortUrl}");
            }
            var author = message == null ? Server.Instance.Assistant.Name : message.DisplayName;
            var fileName = $"[{author}] {title}";
            Server.Instance.saveImageAs(result, fileName, ".png");

            return shortUrl;
        }

        private string generatePromptFromTemplate(string chatPrompt, int maxResponseLength)
        {
            return $"{chatPrompt}. limit {maxResponseLength} words";
        }
    }
}
