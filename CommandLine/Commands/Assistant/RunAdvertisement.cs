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
                    await Server.Instance.twitch.RunAd(AdTime);

                }
                else
                {
                    await Server.Instance.twitch.RunAd(180);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Could not run ad: {e.Message}");
            }
        }
    }
}
