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
            if (voiceProfile != null && chat.Message.Length <= 256)
            {
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
                });
            }
            else
            {
                log.Error($"Message length was too long for tts: {chat.Message.Length} / 256");
            }
        }

        public void PlayRumor(ChatMessage rumor)
        {
            var voiceProfile = VoiceProfiles.GetRumorVoiceProfile();
            if (voiceProfile != null && rumor.Message.Length <= 128)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Server.Instance.elevenlabs.PlayTts(rumor.Message, voiceProfile);
                    }
                    catch (Exception ex)
                    {
                        log.Info("Failed to play sound due to " + ex.Message);
                    }
                });
            }
            else
            {
                log.Info($"Message length was too long for tts: {rumor.Message.Length} / 128");
            }
        }
    }
}
