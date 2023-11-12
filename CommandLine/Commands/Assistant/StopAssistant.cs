namespace TwitchBot.CommandLine.Commands.Assistant
{
    internal class StopAssistant : ServerCommand
    {
        public StopAssistant() : base("stop") { }

        public override void Handle(Server server, string command)
        {
            _ = server.Assistant.StopAI();
        }
    }
}
