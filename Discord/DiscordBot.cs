using Discord.WebSocket;
using Discord;
using static TwitchBot.Config.DiscordConfig;
using TwitchBot.Discord.GamePresence;

namespace TwitchBot.Discord
{
    public class DiscordBot
    {
        public record GamePresence(string Game, string State);

        readonly DiscordSocketClient client;
        public static string LastKnownGame { get; private set; } = "";
        public static string LastKnownState { get; private set; } = "";
        public static GamePresence CurrentPresence { get; private set; } = new("", "");
        static long lastAllowListTime = 0L;
        static long lastTtsTime = 0L;
        static long ttsInterval = 600L;
        private const long SECONDS = 10_000_000L;
        static readonly HashSet<string> statesExperienced = new();
        public static bool Enabled { get; private set; } = true;

        private readonly Logger log = new("Discord");

        private Task DiscordBotLog(LogMessage msg)
        {
            log.Info(msg.ToString(prependTimestamp: false));
            return Task.CompletedTask;
        }

        public static bool IsEnabled()
        {
            return Enabled;
        }

        public DiscordBot(bool enabled = true)
        {
            Enabled = enabled;
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
                foreach (IActivity activity in second.Activities)
                {
                    PlayTtsForActivity(activity);
                }
                return Task.CompletedTask;
            };
        }

        public async Task<IActivity> GetPresence()
        {
            var user = await client.GetUserAsync(Bot.DISCORD_USER_ID);

            if (user != null && user.Activities.Count > 0)
            {
                return user.Activities.First();
            }
            else
            {
                log.Info("Failed to get user or there were no activities.");
                return new Game("", ActivityType.Streaming, ActivityProperties.None, null);
            }
        }

        public async Task<string> GetCurrentGameState()
        {
            if (Enabled)
            {
                var activity = await GetPresence();
                if (activity is RichGame game)
                {
                    return game.State;
                }
            }

            return "";
        }

        public async void PostMessage(ulong channel, string message)
        {
            if (Enabled)
            {
                if (client.GetChannel(channel) is IMessageChannel imageChannel)
                {
                    await imageChannel.SendMessageAsync(message);
                }
            }
        }

        public async void PrintActivity()
        {
            var activity = await GetPresence();
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
                }
            }

            if (activity is RichGame game)
            {
                if (!string.IsNullOrEmpty(game.Name) && !string.IsNullOrEmpty(game.State))
                {
                    RecordStateSeenFromRichPresence(game.Name, game.State);
                    var flavorPrefix = game.State.Split(',')[0];

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
                            if (new Random().Next(5) == 0)
                            {
                                log.Info("Using legacy reaction");
                                Server.Instance.Assistant.ReactToGameState(game.State);
                            }
                            else
                            {
                                log.Info("Using vision reaction");
                                Server.Instance.Assistant.ReactToGameStateAndCurrentScreen(game.State);
                            }
                        }
                    }
                    else
                    {
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

        public async Task<string> UploadFile(string filePath = "images/screenshots/latest.png")
        {
            ulong channel = Config.DiscordConfig.Channel.JustMe.IMAGES;
            if (Enabled)
            {
                if (client.GetChannel(channel) is IMessageChannel imageChannel)
                {
                    var message = await imageChannel.SendFileAsync(filePath);
                    return message.Attachments.First().Url;
                }
            }

            return "";
        }
    }
}
