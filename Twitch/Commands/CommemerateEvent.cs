using TwitchBot.Twitch.Model;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class CommemerateEvent : CommandHandler
    {
        public CommemerateEvent() : base(command: "!commemorate", users: PermissionGroup.User)
        {
            Aliases.Add("!commemoration");
        }

        public override bool MeetsCommandRequirements(ChatMessage message)
        {
            if (Server.Instance.twitch.Enabled && Server.Instance.chatgpt.Enabled && Server.Instance.elevenlabs.Enabled)
            {
                return Server.Instance.twitch.CurrentCommemoration != null && Server.Instance.twitch.CurrentCommemoration.InProgress();
            }

            return false;
        }

        public override void Handle(TwitchIrcBot client, ChatMessage message)
        {
            if (Server.Instance.twitch.CurrentCommemoration != null && Server.Instance.twitch.CurrentCommemoration.InProgress())
            {
                var user = TwitchUser.FromChatMessage(message);
                if (user.UserName != Server.Instance.twitch.CurrentCommemoration.Organizer.UserName)
                {
                    Server.Instance.twitch.CurrentCommemoration.Observers.Add(TwitchUser.FromChatMessage(message));
                }
            }
        }
    }
}
