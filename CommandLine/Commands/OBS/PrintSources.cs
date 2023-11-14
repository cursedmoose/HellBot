namespace TwitchBot.CommandLine.Commands.OBS
{
    internal class PrintSources : ServerCommand
    {
        public PrintSources() : base("obs")
        {
        }

        public override void Handle(Server server, string command)
        {
            server.obs.GetActiveSource();
        }
    }
}
