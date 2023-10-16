using TwitchBot.ChatGpt;
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
        }

        public string Name { get; private set; }
        public VoiceProfile Voice { get; private set; }
        public ObsSceneId Obs { get; private set; }
        protected readonly Logger log;

        private static bool AI_Running = false;
        public abstract string GetSystemPersona();

        public string Persona { get { return GetSystemPersona(); } }

        public abstract void WelcomeBack(string gameTitle);

        public abstract void WelcomeFollower(string username);
        public abstract void WelcomeSubscriber(string username, int length);

        public abstract Task<bool> ChangeTitle();
        public abstract Task<bool> CreatePoll();

        public abstract Task<bool> AnnouncePoll(string title, List<string> options);
        public abstract Task<bool> ConcludePoll(string title, string winner);

        public abstract Task<bool> ChannelRewardClaimed(string byUsername, string rewardTitle, int cost);
        public abstract Task<bool> RunAd(int adSeconds = 5);


        public abstract void CleanUp();

        public void PlayTts(string message)
        {
            Obs.Enable();
            Server.Instance.elevenlabs.PlayTts(message, Voice);
            Obs.Disable();
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

        protected abstract Task AI();

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
                AI_Running = true;
                await Task.Run(Run_AI);
            }
            return;
        }

        public Task StopAI()
        {
            log.Info($"Goodbye at {DateTime.Now}");
            AI_Running = false;
            return Task.CompletedTask;
        }

        public async Task Commemorate(string excitingEvent, ChatMessage? requester = null)
        {
            var image = await Server.Instance.chatgpt.GetImage(excitingEvent, requester);
            if (image != null)
            {
                ObsScenes.LastImage.Enable();
            }
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"commemorate  {excitingEvent}",
                persona: Persona
            );
            ObsScenes.LastImage.Disable();

            return;
        }
    }
}
