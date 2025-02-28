using OllamaSharp;

namespace TwitchBot.Ollama
{
    public class Ollama
    {
        private readonly static Logger log = new("Ollama");
        private static bool Enabled = false;

        private static Uri ollamaUri = new Uri("http://localhost:11434");
        private OllamaApiClient client;

        private Dictionary<string, Chat> chats = new();

        public Ollama(bool enabled = false)
        {
            Enabled = enabled;

            if (!Enabled) { return; }

            client = new(ollamaUri);
            client.SelectedModel = "sheogorath";
        }

        public Chat StartChat(string chatName, string persona)
        {
            if (chats.ContainsKey(chatName)) {
                log.Info($"Making a new chat for {chatName}. This will erase all history from it.");
            }

            chats[chatName] = new Chat(client, persona);
            return chats[chatName];
        }

        public async Task<string> GetResponse(string chatName, string prompt)
        {
            if (!chats.ContainsKey(chatName))
            {
                log.Error($"Chat for {chatName} hasn't been initialized!");
                return "";
            }
            try
            {
                var chat = chats[chatName];
                System.Text.StringBuilder response = new();
                await foreach (var answerToken in chat.SendAsync(prompt))
                {
                    Console.Write(answerToken);
                    response.Append(answerToken);
                }
                return response.ToString();

            }
            catch (Exception ex) 
            {
                log.Error($"{ex.Source}: {ex.Message}");
                return "";
            }

        }


    }
}
