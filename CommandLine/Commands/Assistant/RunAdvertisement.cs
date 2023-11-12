namespace TwitchBot.CommandLine.Commands.Assistant
{
    internal class RunAdvertisement : ServerCommand
    {
        public RunAdvertisement() : base("ad") { }

        public override async void Handle(Server server, string command)
        {
            var timeString = command[2..].Trim();
            try
            {
                if (int.TryParse(timeString, out int AdTime))
                {
                    await server.Assistant.RunAd(AdTime);
                }
                else
                {
                    await server.Assistant.RunAd(180);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Could not run ad: {e.Message}");
            }
        }
    }
}
