using System.Diagnostics;
using TwitchBot.Assistant.AI;
using TwitchBot.ChatGpt;
using TwitchBot.Discord;
using TwitchBot.ElevenLabs;
using TwitchBot.OBS.Scene;
using TwitchBot.ScreenCapture;
using TwitchLib.Client.Models;

namespace TwitchBot.Assistant
{
    public abstract class Assistant
    {
        public Assistant(string name, VoiceProfile voice, ObsSceneId sceneId)
        {
            Name = name;
            Voice = voice;
            Obs = sceneId;
            log = new(Name);
            Agent = new("assistant", Name);
        }

        public static readonly object TtsLock = new();

        public string Name { get; private set; }
        public VoiceProfile Voice { get; private set; }
        public ObsSceneId Obs { get; private set; }
        protected readonly Logger log;
        protected readonly Random Random = new Random();
        protected Dictionary<Actions, DateTime> LastActionTimes = new();
        public readonly FileGenerator.FileGenerator.Agent Agent;
        private bool AI_Running = false;
        public virtual string GetSystemPersona()
        {
            return Name;
        }
        public string Persona { get { return GetSystemPersona(); } }

        public virtual async void WelcomeBack(string gameTitle)
        {
            log.Info($"Oh, welcome back to {gameTitle}...");
            var welcomeBack = $"welcome me back to {gameTitle}";
            await Server.Instance.chatgpt.GetResponse(Persona, welcomeBack);
        }

        public virtual async void WelcomeFollower(string username)
        {
            await Server.Instance.chatgpt.GetResponse(Persona, $"welcome new follower \"{username}\"");
        }

        public virtual async void WelcomeSubscriber(string username, int length)
        {
            await Server.Instance.chatgpt.GetResponse(Persona, $"thank \"{username}\" for subscribing for {length} months");
        }

        public abstract Task<bool> ChangeTitle();
        public abstract Task<bool> CreatePoll(string topic);

        public abstract Task<bool> AnnouncePoll(string title, List<string> options);
        public abstract Task<bool> ConcludePoll(string title, string winner);

        public virtual async Task<bool> ChannelRewardClaimed(string byUsername, string rewardTitle, int cost)
        {
            var prompt = $"react to \"{byUsername}\" redeeming channel reward \"{rewardTitle}\"";
            await Server.Instance.chatgpt.GetResponse(Persona, prompt);
            return true;
        }
        public virtual async Task<bool> RunAd(int adSeconds = 5)
        {
            var run = "announce that it is time for an ad";
            await Server.Instance.chatgpt.GetResponse(Persona, run);
            ObsScenes.Ads.Enable();
            await Server.Instance.twitch.RunAd(adSeconds);
            await Task.Delay((adSeconds + 10) * 1000);
            ObsScenes.Ads.Disable();
            var end = "announce that the ad is over";
            await Server.Instance.chatgpt.GetResponse(Persona, end);

            return true;
        }
        public virtual async Task<int> RollDice(int diceMax = 20)
        {
            foreach (var scene in ObsScenes.AllDice)
            {
                scene.Disable();
            }
            var result = Random.Next(1, diceMax + 1);
            var diceResultScene = ObsScenes.AllDice[result - 1];
            diceResultScene.Enable();
            var text = await Server.Instance.chatgpt.GetResponseText(Persona, $"react to me rolling a {result} out of {diceMax}. limit 15 words");
            ObsScenes.DiceMain.Enable();
            StreamTts(text);
            ObsScenes.DiceMain.Disable();
            diceResultScene.Disable();

            return result;
        }

        public abstract Task CleanUp();

        public void PlayTts(string message)
        {
            var cleanedMessage = Server.Instance.elevenlabs.CleanStringForTts(message);
            var elevenLabsResponse = Server.Instance.elevenlabs.MakeTtsRequest(cleanedMessage, Voice);
            if (elevenLabsResponse.IsSuccessStatusCode)
            {
                lock (TtsLock)
                {
                    Obs.Enable();
                    using Stream responseStream = elevenLabsResponse.Content.ReadAsStream();
                    TtsPlayer.PlayResponseStream(responseStream);
                    Obs.Disable();
                }
            }
        }

        public async Task WaitForSilence()
        {
            while (Server.Instance.speech.IsTalking())
            {
                log.Debug("Waiting for silence...");
                await Task.Delay(250);
            }
        }

        public void StreamTts(string message)
        {
            var cleanedMessage = Server.Instance.elevenlabs.CleanStringForTts(message);
            Server.Instance.elevenlabs.StreamTts(Voice, cleanedMessage, Obs);
        }

        public async Task RespondToPrompt(string prompt)
        {
            var options = new ChatGptOptions(1.33, 2, 2);
            await Server.Instance.chatgpt.GetResponse(Persona, prompt, options);
            return;
        }

        public async Task Chatter()
        {
            var options = new ChatGptOptions(1.33, 2, 2);
            await Server.Instance.chatgpt.GetResponse(Persona, "say anything", options);
            return;
        }

        public async void ReactToGameState(string gameState)
        {
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"react to me {gameState}",
                persona: Persona
            );
        }
        public async void ReactToImage(string imageUrl, string prompt = "give exciting commentary on this image")
        {
            var reaction = await Server.Instance.chatgpt.GetResponseFromImagePrompt(Persona, prompt, imageUrl);
            StreamTts(reaction);
        }

        public async void ReactToGameStateAndCurrentScreen(string gameState)
        {
            var fileUrl = await Server.Instance.TakeAndUploadScreenshot();
            log.Info($"Reacting to {gameState}");
            ReactToImage(fileUrl, $"give exciting commentary on me {gameState}");
        }

        public async void ReactToCurrentState()
        {
            if (!DiscordBot.IsEnabled())
            {
                log.Error("Cannot react to current state without discord running.");
                return;
            }

            var fileUrl = await Server.Instance.TakeAndUploadScreenshot();
            var discord = Task.Run(() => Server.Instance.discord.GetCurrentGameState());
            var twitch = Task.Run(() => Server.Instance.twitch.GetStreamInfo());
            await Task.WhenAll(discord, twitch);

            var discordState = discord.GetAwaiter().GetResult();
            var twitchState = twitch.GetAwaiter().GetResult();

            if (!string.IsNullOrEmpty(discordState))
            {
                ReactToGameStateAndCurrentScreen(discordState);
            } 
            else {
                ReactToGameStateAndCurrentScreen(twitchState.GameName);
            }
            
        }

        public async Task ReactToCurrentScreen()
        {
            await ReactToCurrentScreen("");
        }

        public async Task ReactToCurrentScreen(string prompt = "")
        {
            var reactionPrompt = prompt;

            if (string.IsNullOrEmpty(prompt))
            {
                reactionPrompt = Prompts.Reactions.Random();
            }

            if (Discord.DiscordBot.IsEnabled())
            {
                var fileUrl = await Server.Instance.TakeAndUploadScreenshot();
                ReactToImage(fileUrl, reactionPrompt);
            }
            else
            {
                log.Error("Could not react to current screen as discord is disabled.");
            }
        }

        protected abstract Task AI();
        protected virtual async Task AI_On_Start()
        {
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"welcome back",
                persona: Persona
            );
            await Task.Delay(15 * 1_000);
            return;
        }

        protected virtual async Task AI_On_Stop()
        {
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"goodbye",
                persona: Persona
            );
            return;
        }
        private async Task Run_AI()
        {
            do {
                await AI();
            } while (AI_Running);

            return;
        }

        public async Task StartAI()
        {
            if (!AI_Running)
            {
                log.Info($"Hello at {DateTime.Now}");
                await AI_On_Start();
                AI_Running = true;
                await Task.Run(Run_AI);
            }
            return;
        }

        public bool IsRunning()
        {
            return AI_Running;
        }

        public async Task StopAI()
        {
            log.Info($"Goodbye at {DateTime.Now}");
            AI_Running = false;
            await AI_On_Stop();
            return;
        }

        public async Task Commemorate(string excitingEvent, ChatMessage? requester = null)
        {
            var image = await Server.Instance.chatgpt.GetImage(excitingEvent, requester);
            var agent = requester == null
                ? Agent
                : new FileGenerator.FileGenerator.Agent("user", requester.DisplayName);
            var imageFile = await Server.Instance.file.SaveImage(image, agent);

            if (image != null)
            {
                ObsScenes.LastImage.Enable();
            }
            var commentary = await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"commemorate  {excitingEvent}",
                persona: Persona
            );

            Server.Instance.file.PostToWebsite(agent, new FileGenerator.FileGenerator.Post("commemoration", excitingEvent, imageFile, commentary));
            ObsScenes.LastImage.Disable();

            return;
        }

        public async Task ReadImage(string filePath = ImageFiles.Region)
        {
            var stopwatch = Stopwatch.StartNew();
            var text = await Server.Instance.imageText.ReadText(filePath);
            stopwatch.Stop();
            log.Info($"Model time: {stopwatch.ElapsedMilliseconds}ms");
            StreamTts(text);
        }

        public async Task ReadImage(Bitmap image)
        {
            var stopwatch = Stopwatch.StartNew();
            var text = await Server.Instance.imageText.ReadText(image);
            stopwatch.Stop();
            log.Info($"Model time: {stopwatch.ElapsedMilliseconds}ms");
            StreamTts(text);
        }

        public async void AskAnotherAssistant(Assistant otherAssisant, string prompt)
        {
            var options = new ChatGptOptions(1.33, 2, 2);
            await Server.Instance.chatgpt.GetResponse(otherAssisant.Name, "prompt", options);
        }
    }
}
