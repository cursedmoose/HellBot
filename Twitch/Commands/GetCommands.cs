using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class GetCommands : CommandHandler
    {
        public GetCommands() : base(command: "!commands", users: PermissionGroup.User) { }
        public override void Handle(TwitchIrcBot client, ChatMessage message)
        {
            List<string> usableCommands = new();
            foreach (var command in client.commands)
            {
                if (Permissions.IsUserInGroup(message.Username, command.Users))
                {
                    usableCommands.Add(command.Command);
                }
            }

            client.RespondTo(message, $"@{message.DisplayName} Your available Hellbot commands:\n{string.Join(", ", usableCommands)}");
        }
    }
}
