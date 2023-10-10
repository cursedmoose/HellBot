using System.Text;
using TwitchBot.Assistant.Polls;
using TwitchBot.ElevenLabs;

namespace TwitchBot.Assistant
{
    public abstract class Assistant
    {
        public Assistant(string name, VoiceProfile voice)
        {
            this.Name = name;
            this.Voice = voice;
        }
        protected void Log(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [{Name}] {message}");
        }

        public string Name { get; private set; }
        public VoiceProfile Voice { get; private set; }
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

        public abstract void CleanUp();

        public void PlayTts(string message)
        {
            Server.Instance.elevenlabs.playTts(message, Voice);
        }

        public async Task Chatter()
        {
            await Server.Instance.chatgpt.getResponse(Persona, "say anything");
            return;
        }

        public async void ReactToGameState(string gameState)
        {
            await Server.Instance.chatgpt.getResponse(
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
            Log($"Goodbye at {DateTime.Now}");
            AI_Running = false;
            return Task.CompletedTask;
        }

    }
}
