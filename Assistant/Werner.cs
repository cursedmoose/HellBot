using TwitchBot.ElevenLabs;
using TwitchBot.OBS.Scene;

namespace TwitchBot.Assistant
{
    internal class Werner : Assistant
    {
        public Werner() : base(
                name: "Werner",
                voice: VoiceProfiles.Werner,
                sceneId: ObsScenes.Werner
            )
        {

        }

        public override Task<bool> AnnouncePoll(string title, List<string> options)
        {
            return Task.FromResult(false);
        }

        public override Task<bool> ChangeTitle()
        {
            return Task.FromResult(false);
        }

        public override Task<bool> ChannelRewardClaimed(string byUsername, string rewardTitle, int cost)
        {
            return Task.FromResult(false);
        }

        public override Task CleanUp()
        {
            return Task.CompletedTask;
        }

        public override Task<bool> ConcludePoll(string title, string winner)
        {
            return Task.FromResult(false);
        }

        public override Task<bool> CreatePoll(string topic)
        {
            return Task.FromResult(false);
        }

        public override string GetSystemPersona()
        {
            return "Werner Herzog";
        }

        public override Task<int> RollDice(int diceMax = 20)
        {
            return Task.FromResult(1);
        }

        public override Task<bool> RunAd(int adSeconds = 5)
        {
            return Task.FromResult(false);
        }

        public override void WelcomeBack(string gameTitle)
        {
            return;
        }

        public override void WelcomeFollower(string username)
        {
            return;
        }

        public override void WelcomeSubscriber(string username, int length)
        {
            return;
        }

        protected override Task AI()
        {
            return Task.CompletedTask;
        }

        protected override Task AI_On_Start()
        {
            return Task.CompletedTask;
        }

        protected override Task AI_On_Stop()
        {
            return Task.CompletedTask;
        }
    }
}
