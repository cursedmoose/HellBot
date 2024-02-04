namespace TwitchBot.CommandLine
{
    internal abstract class ServerCommand
    {
        public string Command { get; }
        public List<string> Aliases { get; }

        protected static Logger Log = new("Server");

        protected const StringComparison CompareBy = StringComparison.OrdinalIgnoreCase;

        public ServerCommand(string command)
        {
            Command = command;
            Aliases = new List<string>();
        }

        public bool CanHandle(string command)
        {
            return (MessageStartsWithCommand(command) || MessageStartsWithAlias(command))
                && MeetsCommandRequirements(command);
        }

        public virtual bool MeetsCommandRequirements(string command) { return true; }
        public abstract void Handle(Server server, string command);

        private bool MessageStartsWithCommand(string command)
        {
            return command.StartsWith(Command, CompareBy);
        }

        private bool MessageStartsWithAlias(string command)
        {
            return Aliases.Any((alias) => command.StartsWith(alias, CompareBy));
        }

        protected string StripCommandFromMessage(string command)
        {
            var aliasUsed = Aliases.FirstOrDefault((alias) => command.StartsWith(alias, CompareBy), Command);
            return command.Replace(aliasUsed, string.Empty, CompareBy).Trim();
        }

        public static void ValidateCommandList(List<ServerCommand> commandList)
        {
            Dictionary<string, Type> KnownCommands = new();
            int KeyErrorCount = 0;
            int AliasErrorCount = 0;
            foreach (var command in commandList)
            {
                if (!KnownCommands.ContainsKey(command.Command))
                {
                    KnownCommands[command.Command] = command.GetType();
                }
                else
                {
                    Log.Error($"Command {command.Command} is already present in {KnownCommands[command.Command]}. Cannot add from {command.GetType()}");
                    KeyErrorCount++;
                }

                foreach (var alias in command.Aliases)
                {
                    if (!KnownCommands.ContainsKey(alias))
                    {
                        KnownCommands[alias] = command.GetType();
                    }
                    else
                    {
                        Log.Error($"Alias {alias} is already present in {KnownCommands[alias]}. Cannot add from {command.GetType()}");
                        AliasErrorCount++;
                    }
                }
                
            }
            Log.Info($"Validation complete. {KeyErrorCount} Key Errors. {AliasErrorCount} Alias Errors.");
        }
    }
}
