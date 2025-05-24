namespace TwitchBot.CommandLine.Commands.ServerOptions
{
    internal class ShutdownServer : ServerCommand
    {
        public ShutdownServer() : base("exit")
        {
            Aliases.Add("quit");
            Aliases.Add("shutdown");
        }

        public override void Handle(Server server, string command)
        {
            new ShutdownServer().Handle(server, command);

            server.Assistant.StopAI();
            server.twitch.Stop();
            server.web.Dispose();
            server.obs.Disconnect();
        }
    }
}
