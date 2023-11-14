using DiscordRPC;
using DiscordRPC.Logging;
using static TwitchBot.Config.DiscordConfig;

namespace TwitchBot.Discord
{
    public class DiscordRpc
    {
        readonly DiscordRpcClient client;
        public DiscordRpc()
        {
            client = new DiscordRpcClient(applicationID: Bot.APPLICATION_ID, autoEvents: true, pipe: 0)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning },
            };

            client.OnReady += (sender, msg) =>
            {
                Console.WriteLine("Connected to discord with user {0}", msg.User.Username);
            };

            client.OnPresenceUpdate += (sender, msg) =>
            {
                Console.WriteLine("Presence has been updated! ");
            };


            client.Initialize();
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
