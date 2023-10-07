using TwitchBot.ElevenLabs;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch
{
    internal class TwitchTts
    {
        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [TTS] {message}");
        }
        public TwitchTts() { }
        public void play(ChatMessage chat)
        {
            var voiceProfile = VoiceProfiles.getVoiceProfile(chat.Username);
            if (voiceProfile != null && chat.Message.Length < 256)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Server.Instance.elevenlabs.playTts(chat.Message, voiceProfile);
                    }
                    catch (Exception ex)
                    {
                        Log("Failed to play sound due to " + ex.Message);
                    }
                });
            }
            else
            {
                Log($"Message length was too long for tts: {chat.Message.Length} / 255");
            }
        }

        public void playRumor(ChatMessage rumor)
        {
            var voiceProfile = VoiceProfiles.getRumorVoiceProfile();
            if (voiceProfile != null && rumor.Message.Length < 128)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Server.Instance.elevenlabs.playTts(rumor.Message, voiceProfile);
                    }
                    catch (Exception ex)
                    {
                        Log("Failed to play sound due to " + ex.Message);
                    }
                });
            }
            else
            {
                Log($"Message length was too long for tts: {rumor.Message.Length} / 127");
            }
        }

        private void oldTtsLogic(object sender, OnMessageReceivedArgs e)
        {
            var voiceProfile = VoiceProfiles.getVoiceProfile(e.ChatMessage.Username);
            if (voiceProfile == null && e.ChatMessage.Username != Config.TwitchConfig.Admins.Moose)
            {
                Log($"No allowlist for {e.ChatMessage.Username}");
                if (e.ChatMessage.Message.Length < 128)
                {
                    voiceProfile = VoiceProfiles.getRumorVoiceProfile();
                }
                else
                {
                    Log($"Message was too long to be a rumor ({e.ChatMessage.Message.Length})");
                    return;
                }
            }

            if (e.ChatMessage.Message.Length < 256)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        //PlaySound.Play(e.ChatMessage.Message, e.ChatMessage.Username);
                        Server.Instance.elevenlabs.playTts(e.ChatMessage.Message, voiceProfile);
                    }
                    catch (Exception ex)
                    {
                        Log("Failed to play sound due to " + ex.Message);
                    }
                });
            }
            else
            {
                Log($"Message length was too long for tts: {e.ChatMessage.Message.Length} / 255");
            }
        }
    }
}
