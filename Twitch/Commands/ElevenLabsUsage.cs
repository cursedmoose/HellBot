using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class ElevenLabsUsage : CommandHandler
    {
        public bool canHandle(TwitchClient client, ChatMessage message)
        {
            return message.Message.ToLower().StartsWith("!usage");
        }

        public void handle(TwitchClient client, ChatMessage message)
        {
            var usageInfo = Server.Instance.elevenlabs.getUserSubscriptionInfo();
            client.SendMessage(message.Channel, $"Used {usageInfo.character_count} / {usageInfo.character_limit} characters.");
        }
    }
}
