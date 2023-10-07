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

        public abstract void WelcomeBack(string gameTitle);
        public abstract bool RunPoll(string pollContext);

    }
}
