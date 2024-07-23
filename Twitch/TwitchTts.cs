using TwitchBot.ElevenLabs;
using TwitchBot.OBS.Scene;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch
{
    internal class TwitchTts
    {
        private readonly Logger log = new("TwitchTTS");
        public TwitchTts() { }
        public void Play(ChatMessage chat)
        {
            var voiceProfile = VoiceProfiles.GetVoiceProfile(chat.Username);
            var obsImage = ObsScenes.GetImageSource(chat.Username);
            var message = Server.Instance.elevenlabs.CleanStringForTts(chat.Message);
            if (voiceProfile != null && message.Length <= 256)
            {
                Server.Instance.elevenlabs.StreamTts(voiceProfile, chat.Message, obsImage);
                /* Untested, so commented.
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        obsImage?.Enable();
                        Server.Instance.elevenlabs.PlayTts(chat.Message, voiceProfile);
                        obsImage?.Disable();
                    }
                    catch (Exception ex)
                    {
                        log.Info("Failed to play sound due to " + ex.Message);
                    }
                });*/
            }
            else
            {
                log.Error($"Message length was too long for tts: {message.Length} / 256");
            }
        }

        public void PlayRumor(string rumor)
        {
            var voiceProfile = VoiceProfiles.GetRumorVoiceProfile();
            if (voiceProfile != null)
            {
                Server.Instance.elevenlabs.StreamTts(voiceProfile, rumor);
            }
            else
            {
                log.Error($"Failed to load a voice profile!");
            }
        }
    }
}
