using Discord.WebSocket;
using Discord;
using static TwitchBot.Config.DiscordConfig;

namespace TwitchBot.Discord
{
    public class DiscordBot
    {
        DiscordSocketClient client;
        static string lastKnownState = "";
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
            var things = client.CurrentUser;

            // Log($"Current user is {user.Username}:{user.Discriminator}");
            

            foreach (IActivity activity in user.Activities)
            {
                printActivity(activity);
            }

            return "";
        }

        public SkyrimActivity parsePresence(RichGame game)
        {
            var flavorTokens = game.State.Split(",");
            if (flavorTokens.Length == 3)
            {
                return new SkyrimActivity(flavour: flavorTokens[0].Trim(), location: flavorTokens[1].Trim(), worldspace: flavorTokens[2].Trim());
            } else if (flavorTokens.Length == 2)
            {
                return new SkyrimActivity(flavour: flavorTokens[0].Trim(), location: flavorTokens[1].Trim());
            } else if (flavorTokens.Length == 1)
            {
                return new SkyrimActivity(flavour: flavorTokens[0].Trim(), "");
            } else
            {
                return new SkyrimActivity("", "");
            }
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

        private void playWelcomeBackTts()
        {
            Log($"Oh, welcome back to Skyrim...");
            var welcomeBack = $"welcome me back to Skyrim. limit 25 words.";
            Server.Instance.chatgpt.getResponse("Shegorath", welcomeBack);
        }

        private void playWelcomeBackTts(string game, string persona)
        {
            Log($"Oh, welcome back to {game}...");
            var welcomeBack = $"welcome me back to {game}.";
            Server.Instance.chatgpt.getResponse(persona, welcomeBack, 25);
        }

        private void recordTtsPlayed(long playedAt)
        {
            lastTtsTime = playedAt;
            resetInterval();
            Log($"Next TTS in {ttsInterval}s");
        }

        private void recordStateSeenFromRichPresence(string gameState)
        {
            if (statesExperienced.Add(gameState))
            {
                using (StreamWriter sw = File.AppendText($"{Directory.GetCurrentDirectory()}/log.txt"))
                {
                    sw.WriteLine(gameState);
                }
            }
        }

        private void playTtsForActivity(IActivity activity)
        {
            if (activity is RichGame game)
            {
                if (game.Name == "Skyrim Special Edition" && game.State != null)
                {
                    recordStateSeenFromRichPresence(game.State);
                    var flavorPrefix = game.State.Split(',')[0]; // Catch that
                    var currentTime = DateTime.UtcNow.ToFileTimeUtc();

                    if (lastTtsTime == 0L)
                    {
                        recordTtsPlayed(currentTime);
                        playWelcomeBackTts();
                    }
                    else if (game.State.Length > 0 
                        && RichPresenceConstants.allowedFlavours.Contains(flavorPrefix)
                        && (currentTime - lastAllowListTime) >= (30L * SECONDS)
                        )
                    {
                        Log($"Requested TTS for {game.State} because it was in the allow list.");
                        lastKnownState = game.State;
                        lastAllowListTime = currentTime;
                        recordTtsPlayed(currentTime);
                        Server.Instance.chatgpt.getResponse(getReactRequestPrompt(game.State));
                    } 
                    else if ((currentTime - lastTtsTime) >= (ttsInterval * SECONDS))
                    {
                        Log($"Requested TTS for {game.State} because time interval lapsed.");
                        recordTtsPlayed(currentTime);
                        Server.Instance.chatgpt.getResponse(getReactRequestPrompt(game.State));
                    }
                    else
                    {
                        // Log($"Only {(currentTime - lastTtsTime) / SECONDS} since last TTS. Rate limiting.");
                        return;
                    }
                }
                else if (!String.IsNullOrEmpty(game.Name) && game.State != null)
                {
                    Server.Instance.Assistant.WelcomeBack(game.Name);
                }
            }
        }

        private string getReactRequestPrompt(string gameState)
        {
            return $"{ChatGpt.ChatGpt.getRandomPrompt(Server.Instance.Assistant.Name)} react to me {gameState}. limit 25 words.";
        }

        private long resetInterval()
        {
            ttsInterval = new Random().NextInt64(300L, 600L);
            return ttsInterval;
        }
    }
}
