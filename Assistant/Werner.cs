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
            throw new NotImplementedException();
        }

        public override Task<bool> ChangeTitle()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> ChannelRewardClaimed(string byUsername, string rewardTitle, int cost)
        {
            throw new NotImplementedException();
        }

        public override Task CleanUp()
        {
            return Task.CompletedTask;
        }

        public override Task<bool> ConcludePoll(string title, string winner)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CreatePoll(string topic)
        {
            throw new NotImplementedException();
        }

        public override string GetSystemPersona()
        {
            throw new NotImplementedException();
        }

        public override Task<int> RollDice(int diceMax = 20)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> RunAd(int adSeconds = 5)
        {
            throw new NotImplementedException();
        }

        public override void WelcomeBack(string gameTitle)
        {
            throw new NotImplementedException();
        }

        public override void WelcomeFollower(string username)
        {
            throw new NotImplementedException();
        }

        public override void WelcomeSubscriber(string username, int length)
        {
            throw new NotImplementedException();
        }

        protected override Task AI()
        {
            throw new NotImplementedException();
        }

        protected override Task AI_On_Start()
        {
            throw new NotImplementedException();
        }

        protected override Task AI_On_Stop()
        {
            throw new NotImplementedException();
        }
    }
}
