namespace TwitchBot.CommandLine.Commands
{
    internal class ServiceUsage : ServerCommand
    {
        public ServiceUsage() : base("usage")
        {
        }

        public override void Handle(Server server, string command)
        {
            var info = server.elevenlabs.GetUserSubscriptionInfo();
            Log.Info($"[ElevenLabs] Used {info.character_count} / {info.character_limit} characters.");
            Log.Info($"[ElevenLabs] This instance has used {info.character_count - server.elevenlabs.charactersStartedAt} characters.");

            server.chatgpt.GetUsage();
        }
    }
}
