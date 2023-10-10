using Discord.WebSocket;
using Discord;
using static TwitchBot.Config.DiscordConfig;
using TwitchBot.Discord.Game;
using DiscordRPC;

namespace TwitchBot.Discord
{
    public class DiscordBot
    {
        public record GamePresence(string game, string state);
        
        DiscordSocketClient client;
        static string lastKnownState = "";
        public static GamePresence CurrentPresence = new("", "");
        static long lastAllowListTime = 0L;
        static long lastTtsTime = 0L;
        static long ttsInterval = 600L;
        private const long SECONDS = 10_000_000L;
        static HashSet<string> statesExperienced = new HashSet<string>();
        private static bool isEnabled = true;

        private Task DiscordBotLog(LogMessage msg)
        {
            Log(msg.ToString(prependTimestamp: false));
            return Task.CompletedTask;
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [Discord] {message}");
        }

        public DiscordBot(bool enabled = true)
        {
            isEnabled = enabled;
            DiscordSocketConfig config = new DiscordSocketConfig();
            config.GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences;
            config.AlwaysDownloadUsers = true;
            client = new DiscordSocketClient(config);
            client.Log += DiscordBotLog;

            if (enabled)
            {
                client.LoginAsync(TokenType.Bot, Bot.DISCORD_BOT_TOKEN);
                client.StartAsync();
            }

            client.PresenceUpdated += (SocketUser user, SocketPresence first, SocketPresence second) =>
            {
                // Console.WriteLine($"Received presence updated event from {user.Username}.");
                foreach (IActivity activity in second.Activities)
                {
                    // printActivity(activity);
                    playTtsForActivity(activity);
                }
                return Task.CompletedTask;
            };
        }

        public string getPresence()
        {
            var user = client.GetUser(Bot.DISCORD_USER_ID);
            var user2 = client.GetUserAsync(Bot.DISCORD_USER_ID).GetAwaiter().GetResult();
            var things = client.CurrentUser;

            // Log($"Current user is {user.Username}:{user.Discriminator}");

            if (user != null)
            {
                foreach (IActivity activity in user.Activities)
                {
                    printActivity(activity);
                }
            }
            else
            {
                //Log("Failed to get user.");
            }

            if (user2 != null)
            {
                foreach (IActivity activity in user2.Activities)
                {
                    printActivity(activity);
                }
            }
            else
            {
                Log("Failed to get user2.");
            }



            return "";
        }

        public async void PostMessage(ulong channel, string message)
        {
            if (isEnabled)
            {
                var imageChannel = client.GetChannel(channel) as IMessageChannel;
                if (imageChannel != null)
                {
                    await imageChannel.SendMessageAsync(message);
                }
            }
        }

        private void printActivity(IActivity activity)
        {
            Log("Activity: ");
            if (activity is RichGame game)
            {
                Console.WriteLine($"{game.Type} {game.Name}");
                Console.WriteLine($"{game.State}");
                Console.WriteLine($"Since {game?.Timestamps?.Start?.LocalDateTime.ToString()}");
                Console.WriteLine($"{game?.Details}");
            }
            else
            {
                Console.WriteLine($"{activity.Name}");
                Console.WriteLine($"{activity.Details}");
                Console.WriteLine($"{activity.Type}");
            }
        }

        private void playWelcomeBackTts(string game)
        {
            Log($"Oh, welcome back to {game}...");
            Server.Instance.Assistant.WelcomeBack(game);
        }

        private void recordTtsPlayed(long playedAt)
        {
            lastTtsTime = playedAt;
            resetInterval();
            Log($"Next TTS in {ttsInterval}s");
        }

        private void recordStateSeenFromRichPresence(string game, string gameState)
        {
            CurrentPresence = new(game, gameState);
            if (statesExperienced.Add(gameState))
            {
                using (StreamWriter sw = File.AppendText($"{Directory.GetCurrentDirectory()}/logs/discord/{game}-log.txt"))
                {
                    sw.WriteLine(gameState);
                }
            }
        }

        private void playTtsForActivity(IActivity activity)
        {
            if (activity is RichGame game)
            {
                //if (game.Name == "Skyrim Special Edition" && game.State != null)
                if (!string.IsNullOrEmpty(game.Name) && !string.IsNullOrEmpty(game.State))
                {
                    recordStateSeenFromRichPresence(game.Name, game.State);
                    var flavorPrefix = game.State.Split(',')[0]; // Catch that
                    var currentTime = DateTime.UtcNow.ToFileTimeUtc();

                    if (lastTtsTime == 0L)
                    {
                        recordTtsPlayed(currentTime);
                        playWelcomeBackTts(game.Name);
                    }
                    else if (game.State.Length > 0 
                        && Skyrim.AllowedFlavours.Contains(flavorPrefix)
                        && (currentTime - lastAllowListTime) >= (30L * SECONDS)
                        )
                    {
                        Log($"Requested TTS for {game.State} because it was in the allow list.");
                        lastKnownState = game.State;
                        lastAllowListTime = currentTime;
                        recordTtsPlayed(currentTime);
                        Server.Instance.Assistant.ReactToGameState(game.State);
                    } 
                    else if ((currentTime - lastTtsTime) >= (ttsInterval * SECONDS))
                    {
                        if (!game.State.Contains("menu", StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"Requested TTS for {game.State} because time interval lapsed.");
                            recordTtsPlayed(currentTime);
                            Server.Instance.Assistant.ReactToGameState(game.State);
                        }
                    }
                    else
                    {
                        // Log($"Only {(currentTime - lastTtsTime) / SECONDS} since last TTS. Rate limiting.");
                        return;
                    }
                }
            }
        }

        private long resetInterval()
        {
            ttsInterval = new Random().NextInt64(180L, 360L);
            return ttsInterval;
        }
    }
}
