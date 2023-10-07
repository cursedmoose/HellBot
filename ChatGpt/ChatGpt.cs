using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using TwitchBot.ElevenLabs;
using TwitchLib.Client.Models;
using static TwitchBot.ChatGpt.Constants;
using System.Globalization;
using OpenAI.Models;
using static TwitchBot.Config.ChatGptConfig;
using static TwitchBot.Config.DiscordConfig;

namespace TwitchBot.ChatGpt
{
    public class ChatGpt
    {
        OpenAIClient openAI;
        bool isEnabled;

        public ChatGpt(bool enabled = true)
        {
            openAI = new(openAIAuthentication: new(API_KEY, ORGANIZATION_ID));
            isEnabled = enabled;
        }

        public static string getRandomPrompt(string forPersona)
        {
            var choice = new Random().Next(0, personaPrompts.Count);
            return string.Format(personaPrompts[choice], forPersona);
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [ChatGpt] {message}");
        }

        private async Task<bool> requestResponses(List<Message> messages)
        {
            var chatRequest = new ChatRequest(
                model: Model.GPT3_5_Turbo,
                messages: messages, 
                temperature: 1.5);

            try
            {
                var result = await openAI.ChatEndpoint.GetCompletionAsync(chatRequest);
                Server.Instance.elevenlabs.playTts(result.FirstChoice, VoiceProfiles.Sheogorath);
            }
            catch (Exception e)
            {
                Log($"Got error: {e.Message}");
            }

            return true;
        }

        private async Task<string> requestResponseText(List<Message> messages)
        {
            var chatRequest = new ChatRequest(
                model: Model.GPT3_5_Turbo,
                messages: messages,
                temperature: 1.5);
            var responseText = "";
            try
            {
                var result = await openAI.ChatEndpoint.GetCompletionAsync(chatRequest);
                responseText = result.FirstChoice;
            }
            catch (Exception e)
            {
                Log($"Got error: {e.Message}");
            }

            return responseText;
        }

        public async Task<string> getResponseText(string chatPrompt, int maxResponseLength = 25)
        {
            if (!isEnabled) { return ""; }

            Log($"Asking ChatGpt to respond to {chatPrompt}");
            // string promptTemplate = $"{getRandomPrompt("Sheogorath")} react to me {chatPrompt}. limit {maxTokens} words.";
            string promptTemplate = generatePromptFromTemplate(chatPrompt, maxResponseLength);

            var chatPrompts = new List<Message>
            {
                new(Role.User, promptTemplate)
            };
            //var chatRequest = new ChatRequest(messages: chatPrompts, maxTokens: 50, temperature: 1.5);

            return await requestResponseText(chatPrompts);
        }

        public async Task<string> getResponseText(string persona, string chatPrompt, int maxResponseLength = 25)
        {
            if (!isEnabled) { return ""; }

            Log($"Asking ChatGpt to respond to {chatPrompt}");
            // string promptTemplate = $"{getRandomPrompt("Sheogorath")} react to me {chatPrompt}. limit {maxTokens} words.";
            string promptTemplate = generatePromptFromTemplate(chatPrompt, maxResponseLength);

            var chatPrompts = new List<Message>
            {
                new(Role.System, getRandomPrompt(persona)),
                new(Role.User, promptTemplate)
            };
            //var chatRequest = new ChatRequest(messages: chatPrompts, maxTokens: 50, temperature: 1.5);

            return await requestResponseText(chatPrompts);
        }

        public async void getResponse(string chatPrompt, int maxResponseLength = 25)
        {
            if (!isEnabled) { return; }

            Log($"Asking ChatGpt to respond to {chatPrompt}.");
            string promptTemplate = generatePromptFromTemplate(chatPrompt, maxResponseLength);

            var chatPrompts = new List<Message>
            {
                new(Role.User, promptTemplate)
            };

            await requestResponses(chatPrompts);
        }

        public async void getResponse(string persona, string chatPrompt, int maxResponseLength = 25)
        {
            if (!isEnabled) { return; }

            Log($"Asking ChatGpt to respond to {chatPrompt}");
            string promptTemplate = generatePromptFromTemplate(chatPrompt, maxResponseLength);

            Log(promptTemplate);

            var chatPrompts = new List<Message>
            {
                new(Role.System, getRandomPrompt(persona)),
                new(Role.User, promptTemplate)
            };

            await requestResponses(chatPrompts);
        }

        public async Task<string> getImage(string imagePrompt, ChatMessage? message = null)
        {
            var results = await openAI.ImagesEndPoint.GenerateImageAsync(imagePrompt, 1, ImageSize.Medium);
            var result = results.First();

            Log($"Generated image: {result}");
            var title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(imagePrompt);
            Server.Instance.discord.PostMessage(Channel.JustMe.IMAGES, $"\"{title}\":");
            Server.Instance.discord.PostMessage(Channel.JustMe.IMAGES, $"{result}");

            if (message != null) {
                Server.Instance.twitch.RespondTo(message, result);
            }

            Server.Instance.saveAs(result, title, ".png");

            return result;
        }

        private string generatePromptFromTemplate(string chatPrompt, int maxResponseLength)
        {
            return $"{chatPrompt}. limit {maxResponseLength} words";
        }
    }
}
