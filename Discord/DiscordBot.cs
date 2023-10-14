using Discord.WebSocket;
using Discord;
using static TwitchBot.Config.DiscordConfig;
using TwitchBot.Discord.Game;

namespace TwitchBot.Discord
{
    public class DiscordBot
    {
        public record GamePresence(string Game, string State);
        
        readonly DiscordSocketClient client;
        public static string LastKnownGame { get;  private set; } = "";
        public static string LastKnownState { get; private set; } = "";
        public static GamePresence CurrentPresence { get; private set; } = new("", "");
        static long lastAllowListTime = 0L;
        static long lastTtsTime = 0L;
        static long ttsInterval = 600L;
        private const long SECONDS = 10_000_000L;
        static readonly HashSet<string> statesExperienced = new();
        private static bool isEnabled = true;

        private readonly Logger log = new("Discord");

        private Task DiscordBotLog(LogMessage msg)
        {
            log.Info(msg.ToString(prependTimestamp: false));
            return Task.CompletedTask;
        }

        public DiscordBot(bool enabled = true)
        {
            isEnabled = enabled;
            DiscordSocketConfig config = new()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences,
                AlwaysDownloadUsers = true
            };
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
                    PlayTtsForActivity(activity);
                }
                return Task.CompletedTask;
            };
        }

        public void GetPresence()
        {
            var user = client.GetUser(Bot.DISCORD_USER_ID);

            if (user != null)
            {
                foreach (IActivity activity in user.Activities)
                {
                    log.Info("Presence: ");
                    PrintActivity(activity);
                }
            }
            else
            {
                log.Info("Failed to get user.");
            }
        }

        public async void PostMessage(ulong channel, string message)
        {
            if (isEnabled)
            {
                if (client.GetChannel(channel) is IMessageChannel imageChannel)
                {
                    await imageChannel.SendMessageAsync(message);
                }
            }
        }

        private void PrintActivity(IActivity activity)
        {
            log.Info($"Last Known Game:  { LastKnownGame }");
            log.Info($"Last Known State: { LastKnownState }");

            log.Info("Current Activity: ");
            if (activity is RichGame game)
            {
                Console.WriteLine("Rich Game!");
                Console.WriteLine($"Type: {game.Type} | Name: {game.Name}");
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

        private void RecordTtsPlayed(long playedAt)
        {
            lastTtsTime = playedAt;
            ResetInterval();
            log.Info($"Next TTS in {ttsInterval}s");
        }

        private static void RecordStateSeenFromRichPresence(string game, string gameState)
        {
            CurrentPresence = new(game, gameState);
            if (statesExperienced.Add(gameState))
            {
                using StreamWriter sw = File.AppendText($"{Directory.GetCurrentDirectory()}/logs/discord/{game}-log.txt");
                sw.WriteLine(gameState);
            }
        }

        private void PlayTtsForActivity(IActivity activity)
        {
            var currentTime = DateTime.UtcNow.ToFileTimeUtc();

            if (activity.Type == ActivityType.Playing)
            {
                if (lastTtsTime == 0L || (activity.Name != LastKnownGame && !string.IsNullOrEmpty(activity.Name)))
                {
                    RecordTtsPlayed(currentTime);
                    LastKnownGame = activity.Name;
                    var sanitizedName = activity.Name.Replace("Demo", "").Trim();
                    Server.Instance.twitch.ChangeGame(sanitizedName).GetAwaiter().GetResult();
                    Server.Instance.Assistant.WelcomeBack(sanitizedName);
                }
            }

            if (activity is RichGame game)
            {
                //if (game.Name == "Skyrim Special Edition" && game.State != null)
                if (!string.IsNullOrEmpty(game.Name) && !string.IsNullOrEmpty(game.State))
                {
                    RecordStateSeenFromRichPresence(game.Name, game.State);
                    var flavorPrefix = game.State.Split(',')[0]; // Catch that

                    if (game.State.Length > 0 
                        && Skyrim.AllowedFlavours.Contains(flavorPrefix)
                        && (currentTime - lastAllowListTime) >= (30L * SECONDS)
                        )
                    {
                        log.Info($"Requested TTS for {game.State} because it was in the allow list.");
                        lastAllowListTime = currentTime;
                        RecordTtsPlayed(currentTime);
                        LastKnownState = game.State;
                        Server.Instance.Assistant.ReactToGameState(game.State);
                    } 
                    else if ((currentTime - lastTtsTime) >= (ttsInterval * SECONDS))
                    {
                        if (!game.State.Contains("menu", StringComparison.OrdinalIgnoreCase))
                        {
                            log.Info($"Requested TTS for {game.State} because time interval lapsed.");
                            RecordTtsPlayed(currentTime);
                            LastKnownState = game.State;
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

        private static long ResetInterval()
        {
            ttsInterval = new Random().NextInt64(180L, 360L);
            return ttsInterval;
        }
    }
}
