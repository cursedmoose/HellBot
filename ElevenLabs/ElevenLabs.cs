using static TwitchBot.Config.ElevenLabsConfig;

namespace TwitchBot.ElevenLabs
{
    public class ElevenLabs
    {
        readonly HttpClient client;
        public readonly long charactersStartedAt;
        public static long CharactersUsed { get; private set; } = 0;
        public readonly bool Enabled = true;
        readonly Logger log = new("ElevenLabs");

        public ElevenLabs(bool enabled = true)
        {
            Enabled = enabled;
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("xi-api-key", API_KEY);
            charactersStartedAt = GetUserSubscriptionInfo().character_count;
        }

        public void PlayTts(string ttsMessage, VoiceProfile? voiceProfile)
        {
            if (!Enabled || voiceProfile == null || string.IsNullOrWhiteSpace(ttsMessage))
            {
                return;
            }


            try
            {
                TtsPlayer.Play(ttsMessage, voiceProfile);
            } catch (Exception e)
            {
                log.Info($"Exception trying to play TTS: {e.Message}");
            }
            finally
            {
                CharactersUsed += ttsMessage.Length;
            }
        }

        public SubscriptionInfoResponse GetUserSubscriptionInfo()
        {
            return SubscriptionInfo.call(client);
        }
    }
}
