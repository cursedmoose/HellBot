using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using TwitchLib.Client.Models;
using System.Globalization;
using OpenAI.Models;
using static TwitchBot.Config.ChatGptConfig;
using static TwitchBot.Config.DiscordConfig;
using Message = OpenAI.Chat.Message;
using TwitchBot.Twitch.Model;

namespace TwitchBot.ChatGpt
{
    public record ChatGptOptions(
        double? Temperature,
        double? FrequencyPenalty,
        double? PresencePenalty
        )
    {
        public static readonly ChatGptOptions Default = new(1.25, 0, 0);
        public static readonly ChatGptOptions Vision = new(1.25, 0, 0);
    }
    public class ChatGpt
    {
        readonly OpenAIClient openAI;
        static readonly ChatGptUsage usage = new();
        public readonly bool Enabled;
        readonly Logger log = new("ChatGpt");

        public ChatGpt(bool enabled = true)
        {
            openAI = new(openAIAuthentication: new(API_KEY, ORGANIZATION_ID));
            Enabled = enabled;
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
                Server.Instance.Assistant.StreamTts(result);
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
            ChatGptOptions api_params = options ?? ChatGptOptions.Default;
            var chatRequest = new ChatRequest(
                model: "gpt-4o", //Model.GPT4,
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
            if (!Enabled) { return ""; }

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
            if (!Enabled) { return ""; }

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
            if (!Enabled) { return ""; }

            log.Info($"Asking ChatGpt to respond to {chatPrompt}");
            string promptTemplate = GeneratePromptFromTemplate(chatPrompt, 25);

            log.Info(promptTemplate);

            var chatPrompts = new List<Message>
            {
                new(Role.System, persona),
                new(Role.User, promptTemplate)
            };

            return await RequestResponses(chatPrompts, options);
        }

        public async Task<string> GetImage(string imagePrompt, TwitchUser? forUser = null)
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

            if (forUser != null) {
                Server.Instance.twitch.RespondTo(forUser, $"{title}: {shortUrl}");
            }

            return shortUrl;
        }

        private static string GeneratePromptFromTemplate(string chatPrompt, int maxResponseLength)
        {
            return $"{chatPrompt}. limit {maxResponseLength} words";
        }

        public async Task<string> GetResponseFromImagePrompt(string persona, string prompt = "What's in this image?", string imageUrl = "https://i.imgur.com/En3mezF.jpeg")
        {
            log.Info($"looking at {imageUrl}");
            var messages = new List<Message>
            {
                new Message(Role.System, persona),
                new Message(Role.User, new List<Content>
                {
                    new Content(ContentType.Text, GeneratePromptFromTemplate(prompt, 35)),
                    new Content(ContentType.ImageUrl, imageUrl)
                })
            };
            return await GetVisionResponse(messages);
        }

        public async Task<string> ExtractTextFromImage(string imageUrl = "https://i0.wp.com/bloody-disgusting.com/wp-content/uploads/2018/10/AF.jpg?w=640&ssl=1")
        {
            string prompt = "Only return the text in this image";
            log.Info($"looking at {imageUrl}");
            var messages = new List<Message>
            {
                new Message(Role.User, new List<Content>
                {
                    new Content(ContentType.Text, GeneratePromptFromTemplate(prompt, 255)),
                    new Content(ContentType.ImageUrl, imageUrl)
                })
            };
            return await GetVisionResponse(messages);
        }

        private async Task<string> GetVisionResponse(List<Message> messages)
        {
            ChatGptOptions api_params = ChatGptOptions.Vision;
            var chatRequest = new ChatRequest(
                messages: messages,
                model: "gpt-4o",
                temperature: api_params.Temperature,
                presencePenalty: api_params.PresencePenalty,
                frequencyPenalty: api_params.FrequencyPenalty,
                maxTokens: 300);
            try
            {
                var result = await openAI.ChatEndpoint.GetCompletionAsync(chatRequest);
                usage.recordUsage(result.Usage);

                return result.FirstChoice;
            }
            catch (Exception ex)
            {
                log.Error($"Could not process request: {ex.Message}");
                return "";
            }
        }
    }
}
