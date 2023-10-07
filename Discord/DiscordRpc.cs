using DiscordRPC;
using DiscordRPC.Logging;
using static TwitchBot.Config.DiscordConfig;

namespace TwitchBot.Discord
{
    public class DiscordRpc
    {
        private static RichPresence presence = new RichPresence()
        {
            Details = "Test Project 🎁",
            State = "testing something",
            Assets = new Assets()
            {
                LargeImageKey = "image_large",
                LargeImageText = "test",
                SmallImageKey = "image_small"
            }
        };

        DiscordRpcClient client;
        public DiscordRpc()
        {
            client = new DiscordRpcClient(applicationID: Bot.APPLICATION_ID, autoEvents: true, pipe: 0)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning },
            };

            client.OnReady += (sender, msg) =>
            {
                //Create some events so we know things are happening
                Console.WriteLine("Connected to discord with user {0}", msg.User.Username);
            };

            client.OnPresenceUpdate += (sender, msg) =>
            {
                //The presence has updated
                Console.WriteLine("Presence has been updated! ");
            };


            client.Initialize();
        }

        public string getPresence()
        {
            return "";
        }

        public void Start()
        {
            client.Initialize();
        }

        public void Stop()
        {
            client.Dispose();
        }
    }
}
