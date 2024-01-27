namespace TwitchBot.CommandLine.Commands.Assistant
{
    internal class Narrate : ServerCommand
    {
        public Narrate() : base("narrate") {
            Aliases.Add("stream");
        }

        public override void Handle(Server server, string command)
        {
            var prompt = StripCommandFromMessage(command);
            server.Narrator.StreamTts(prompt);
        }
    }
}
