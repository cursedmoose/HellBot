using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class CreatePoll : CommandHandler
    {
        public CreatePoll() : base (command: "!poll", users: PermissionGroup.Admin) { }

        public override void Handle(TwitchIrcBot client, ChatMessage message)
        {
            var pollTopic = StripCommandFromMessage(message);
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
