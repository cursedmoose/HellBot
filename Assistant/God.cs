using TwitchBot.ElevenLabs;
using TwitchBot.OBS.Scene;

namespace TwitchBot.Assistant
{
    public class God : Assistant
    {
        public God() : base(
                name: "God",
                voice: VoiceProfiles.God,
                sceneId: ObsScenes.God
            )
        {

        }
        protected override Task AI()
        {
            return Task.CompletedTask;
        }
        public override Task CleanUp()
        {
            return Task.CompletedTask;
        }

        public override Task<bool> CreatePoll(string topic)
        {
            return Task.FromResult(false);
        }

        public override Task<bool> AnnouncePoll(string title, List<string> options)
        {
            return Task.FromResult(false);
        }

        public override Task<bool> ConcludePoll(string title, string winner)
        {
            return Task.FromResult(false);
        }

        public override Task<bool> ChangeTitle()
        {
            return Task.FromResult(false);
        }
    }
}
