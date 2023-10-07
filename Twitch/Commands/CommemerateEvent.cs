using TwitchBot.ElevenLabs;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class CommemerateEvent : CommandHandler
    {
        const string COMMAND = "!commemorate";
        public bool canHandle(TwitchClient client, ChatMessage message)
        {
            var validUsername = VoiceProfiles.getVoiceProfile(message.Username) != null;
            return message.Message.StartsWith(COMMAND) && (validUsername);
        }

        public void handle(TwitchClient client, ChatMessage message)
        {
            var imageRequest = message.Message.Replace(COMMAND, string.Empty).Trim();
            if (imageRequest.Length > 0)
            {
                Console.WriteLine($"Calling getImage with param {imageRequest}");
                Server.Instance.chatgpt.getImage(imageRequest, message).GetAwaiter().GetResult();

                var author = message.Username;
            }
        }
    }
}
