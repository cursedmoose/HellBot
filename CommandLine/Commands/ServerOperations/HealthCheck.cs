namespace TwitchBot.CommandLine.Commands.ServerOptions
{
    internal class HealthCheck : ServerCommand
    {
        public HealthCheck() : base("health")
        {
            Aliases.Add("status");
        }

        public override void Handle(Server server, string command)
        {
            var AI_Status = server.Assistant.IsRunning()
                ? "[Assistant] is running"
                : "[Assistant] is NOT running";
            Log.Info(AI_Status);
            Log.Info($"Twitch Enabled: [{server.twitch.Enabled}]");
            server.twitch.HealthCheck();
        }
    }
}
