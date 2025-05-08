namespace TwitchBot.CommandLine.Commands
{
    // Generally for testing new functionality
    internal class Commemorate : ServerCommand
    {
        public Commemorate() : base("commemorate")
        {
        }
        public override async void Handle(Server server, string command)
        {
            var prompt = StripCommandFromMessage(command);
            await Server.Instance.twitch.Commemorate("cursedmoose", prompt);
        }
    }
}