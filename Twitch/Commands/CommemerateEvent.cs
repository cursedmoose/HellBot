using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class CommemerateEvent : CommandHandler
    {
        public CommemerateEvent() : base(command: "!commemorate", users: PermissionGroup.Admin)
        {
            Aliases.Add("!commemoration");
        }

        public override bool MeetsCommandRequirements(ChatMessage message)
        {
            return Server.Instance.chatgpt.Enabled;
        }

        public override async void Handle(TwitchIrcBot client, ChatMessage message)
        {
            var imageRequest = StripCommandFromMessage(message);

            if (imageRequest.Length > 0)
            {
                await Server.Instance.Assistant.Commemorate(imageRequest, message);
            }
        }
    }
}
