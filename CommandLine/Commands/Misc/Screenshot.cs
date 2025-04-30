namespace TwitchBot.CommandLine.Commands
{
    // Generally for testing new functionality
    internal class Screenshot : ServerCommand
    {
        public Screenshot() : base("screenshot")
        {
        }
        public override async void Handle(Server server, string command)
        {
            await Server.Instance.TakeAndUploadScreenshot();
        }
    }
}