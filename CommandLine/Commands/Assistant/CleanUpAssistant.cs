namespace TwitchBot.CommandLine.Commands.Assistant
{
    internal class CleanUpAssistant : ServerCommand
    {
        public CleanUpAssistant() : base("clean") { }

        public override void Handle(Server server, string command)
        {
            server.Assistant.CleanUp();
        }
    }
}
