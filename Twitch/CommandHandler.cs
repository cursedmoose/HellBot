using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch
{
    internal abstract class CommandHandler
    {
        public string Command { get; }
        public List<string> Aliases { get; }
        public PermissionGroup Users { get; }

        protected const StringComparison CompareBy = StringComparison.OrdinalIgnoreCase;

        public CommandHandler(string command, PermissionGroup users)
        {
            Command = command;
            Users = users;
            Aliases = new List<string>();
        }

        public bool CanHandle(ChatMessage message)
        {
            return (MessageStartsWithCommand(message) || MessageStartsWithAlias(message))
                && UserCanUseCommand(message)
                && MeetsCommandRequirements(message);
        }

        public virtual bool MeetsCommandRequirements(ChatMessage message) { return true; }
        public abstract void Handle(TwitchIrcBot client, ChatMessage message);

        private bool MessageStartsWithCommand(ChatMessage message)
        {
            return message.Message.StartsWith(Command, CompareBy);
        }

        private bool MessageStartsWithAlias(ChatMessage message)
        {
            return Aliases.Any((command) => message.Message.StartsWith(command, CompareBy));
        }

        private bool UserCanUseCommand(ChatMessage message) 
        {
            return Permissions.IsUserInGroup(message.Username, Users);
        }

        protected string StripCommandFromMessage(ChatMessage message)
        {
            var aliasUsed = Aliases.FirstOrDefault((command) => message.Message.StartsWith(command, CompareBy), Command);
            return message.Message.Replace(Command, string.Empty, CompareBy).Trim();
        }
    }
}
