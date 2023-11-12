namespace TwitchBot.CommandLine.Commands.Assistant
{
    internal class StartAssistant : ServerCommand
    {
        public StartAssistant() : base("start") { }

        public override void Handle(Server server, string command)
        {
            _ = server.Assistant.StartAI();
        }
    }
}
