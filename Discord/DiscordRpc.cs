using DiscordRPC;
using DiscordRPC.Logging;
using static TwitchBot.Config.DiscordConfig;

namespace TwitchBot.Discord
{
    public class DiscordRpc
    {
        readonly DiscordRpcClient client;
        readonly Logger log = new("DiscordRpc");
        public DiscordRpc()
        {
            client = new DiscordRpcClient(applicationID: Bot.APPLICATION_ID, autoEvents: true, pipe: 0)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning },
            };

            client.OnReady += (sender, msg) =>
            {
                log.Info($"Connected to discord with user {msg.User.Username}");
            };

            client.OnPresenceUpdate += (sender, msg) =>
            {
                log.Debug("Presence has been updated!");
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
