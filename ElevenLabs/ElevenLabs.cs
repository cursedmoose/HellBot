using static TwitchBot.Config.ElevenLabsConfig;

namespace TwitchBot.ElevenLabs
{
    public class ElevenLabs
    {
        HttpClient client;
        public readonly long charactersStartedAt;
        static long charactersUsed = 0;

        public ElevenLabs(bool enabled = true)
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("xi-api-key", API_KEY);
            charactersStartedAt = getUserSubscriptionInfo().character_count;
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [ElevenLabs] {message}");
        }

        public void playTts(string ttsMessage, string voiceName = "dagoth")
        {
            VoiceProfile voiceProfile = VoiceProfiles.getVoiceProfile(voiceName);
            if (voiceProfile == null)
            {
                Log($"No voice profile set for {voiceName}");
                return;
            }
            
            playTts(ttsMessage, voiceProfile);
        }

        public void playTts(string ttsMessage, VoiceProfile voiceProfile)
        {
            try
            {
                PlayTts.Play(ttsMessage, voiceProfile);
            } catch (Exception e)
            {
                Log($"Exception trying to play TTS: {e.Message}");
            }
            finally
            {
                charactersUsed += ttsMessage.Length;
            }
        }

        public SubscriptionInfoResponse getUserSubscriptionInfo()
        {
            return SubscriptionInfo.call(client);
        }

    }
}
