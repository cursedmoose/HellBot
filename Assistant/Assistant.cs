using TwitchBot.ChatGpt;
using TwitchBot.Discord;
using TwitchBot.ElevenLabs;
using TwitchBot.OBS.Scene;
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

        static readonly object TtsLock = new();

        public string Name { get; private set; }
        public VoiceProfile Voice { get; private set; }
        public ObsSceneId Obs { get; private set; }
        protected readonly Logger log;
        protected readonly Random Random = new Random();

        public readonly FileGenerator.FileGenerator.Agent Agent;

        private bool AI_Running = false;
        public abstract string GetSystemPersona();

        public string Persona { get { return GetSystemPersona(); } }

        public abstract void WelcomeBack(string gameTitle);

        public abstract void WelcomeFollower(string username);
        public abstract void WelcomeSubscriber(string username, int length);

        public abstract Task<bool> ChangeTitle();
        public abstract Task<bool> CreatePoll(string topic);

        public abstract Task<bool> AnnouncePoll(string title, List<string> options);
        public abstract Task<bool> ConcludePoll(string title, string winner);

        public abstract Task<bool> ChannelRewardClaimed(string byUsername, string rewardTitle, int cost);
        public abstract Task<bool> RunAd(int adSeconds = 5);

        public abstract Task<int> RollDice(int diceMax = 20);


        public abstract void CleanUp();

        public void PlayTts(string message)
        {
            lock (TtsLock) {
                Obs.Enable();
                Server.Instance.elevenlabs.PlayTts(message, Voice);
                Obs.Disable();
            }
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
            PlayTts(reaction);
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
            if (Discord.DiscordBot.IsEnabled())
            {
                var fileUrl = await Server.Instance.TakeAndUploadScreenshot();
                ReactToImage(fileUrl);
            }
            else
            {
                log.Error("Could not react to current screen as discord is disabled.");
            }
        }

        protected abstract Task AI();
        protected abstract Task AI_On_Start();
        protected abstract Task AI_On_Stop();
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
    }
}
