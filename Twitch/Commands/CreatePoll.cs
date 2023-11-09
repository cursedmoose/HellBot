using TwitchBot.ElevenLabs;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class CreatePoll : CommandHandler
    {
        const string COMMAND = "!poll";

        public bool canHandle(TwitchClient client, ChatMessage message)
        {
            var validUsername = VoiceProfiles.GetVoiceProfile(message.Username) != null;
            return message.Message.StartsWith(COMMAND) && validUsername;
        }

        public void handle(TwitchClient client, ChatMessage message)
        {
            var pollTopic = message.Message.Replace(COMMAND, string.Empty).Trim();
            if (pollTopic.Length > 0)
            {
                var pollMade = Server.Instance.Assistant.CreatePoll(pollTopic).Result;
                if (!pollMade)
                {
                    Server.Instance.twitch.RespondTo(message, $"Error making poll about {pollTopic}");
                }
            }
        }
    }
}
