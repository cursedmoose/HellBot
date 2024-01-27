namespace TwitchBot.CommandLine.Commands.Assistant
{
    internal class PlayTts : ServerCommand
    {
        public PlayTts() : base("tts")
        {
            Aliases.Add("say");
        }

        public override void Handle(Server server, string command)
        {
            var prompt = StripCommandFromMessage(command);
            server.Assistant.PlayTts(prompt);
        }
    }
}
