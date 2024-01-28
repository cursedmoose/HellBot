namespace TwitchBot.CommandLine.Commands.ServerOptions
{
    internal class StartSubroutine : ServerCommand
    {
        public static Dictionary<string, Func<Task>> ComponentsNeedingStartup = new();

        public StartSubroutine() : base("start")
        {
            ComponentsNeedingStartup.Add("assistant", Server.Instance.Assistant.StartAI);
            ComponentsNeedingStartup.Add("scraper", Server.Instance.screen.StartScraper); 
        }

        public override void Handle(Server server, string command)
        {
            var options = StripCommandFromMessage(command);
            if (string.IsNullOrEmpty(options))
            {
                foreach (var component in ComponentsNeedingStartup)
                {
                    Log.Info($"Starting {component.Key}");
                    component.Value.Invoke();
                }
            }
            else
            {
                try
                {
                    ComponentsNeedingStartup[options].Invoke();
                }
                catch
                {
                    Log.Info($"No way to invoke {options}");
                }
            }
        }
    }
}
