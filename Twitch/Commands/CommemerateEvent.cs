using TwitchBot.ElevenLabs;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class CommemerateEvent : CommandHandler
    {
        const string COMMAND = "!commemorate";
        const string ALT_COMMAND = "!commemoration";
        public bool canHandle(TwitchClient client, ChatMessage message)
        {
            var validUsername = VoiceProfiles.GetVoiceProfile(message.Username) != null;
            return (message.Message.StartsWith(COMMAND) || message.Message.StartsWith(ALT_COMMAND))
                && validUsername;
        }

        public void handle(TwitchClient client, ChatMessage message)
        {
            var imageRequest = message.Message.Replace(COMMAND, string.Empty).Trim();
            if (imageRequest.Length > 0)
            {
                Server.Instance.Assistant.Commemorate(imageRequest, message).GetAwaiter().GetResult();
            }
        }
    }
}
