namespace TwitchBot.CommandLine.Commands.ServerOptions
{
    internal class StopSubroutine : ServerCommand
    {
        public static Dictionary<string, Func<Task>> ComponentsNeedingShutdown = new();

        public StopSubroutine() : base("stop") 
        {
            ComponentsNeedingShutdown.Add("assistant", Server.Instance.Assistant.StopAI);
            ComponentsNeedingShutdown.Add("scraper", Server.Instance.screen.StopScraper);
            ComponentsNeedingShutdown.Add("brain", Server.Instance.brain.Stop);
        }

        public override void Handle(Server server, string command)
        {
            var options = StripCommandFromMessage(command);
            if (string.IsNullOrEmpty(options))
            {
                foreach (var component in ComponentsNeedingShutdown)
                {
                    Log.Info($"Stopping {component.Key}");
                    component.Value.Invoke();
                }

            }
            else
            {
                try
                {
                    ComponentsNeedingShutdown[options].Invoke();
                }
                catch
                {
                    Log.Info($"No way to stop {options}");
                }
            }
        }
    }
}
