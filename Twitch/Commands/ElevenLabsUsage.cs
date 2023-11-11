using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class ElevenLabsUsage : CommandHandler
    {
        public ElevenLabsUsage() : base(command: "!usage", users: PermissionGroup.User) { }

        public override void Handle(TwitchIrcBot client, ChatMessage message)
        {
            var usageInfo = Server.Instance.elevenlabs.GetUserSubscriptionInfo();
            client.RespondTo(message, $"Used {usageInfo.character_count} / {usageInfo.character_limit} characters.");
        }
    }
}
