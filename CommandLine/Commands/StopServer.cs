namespace TwitchBot.CommandLine.Commands
{
    internal class StopServer : ServerCommand
    {
        public StopServer() : base("exit")
        {
            Aliases.Add("quit");
            Aliases.Add("shutdown");
        }

        public override void Handle(Server server, string command)
        {
            server.Assistant.StopAI().GetAwaiter().GetResult();
            server.Assistant.CleanUp();
            server.twitch.Stop();
            server.web.Dispose();
            server.obs.Disconnect();
        }
    }
}
