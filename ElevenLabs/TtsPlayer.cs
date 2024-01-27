using NAudio.Wave;
using System.Diagnostics;
using System.Media;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using static TwitchBot.Config.ElevenLabsConfig;

namespace TwitchBot.ElevenLabs
{
    internal class TtsPlayer
    {
        static int counter;

        const string TTS_API = "https://api.elevenlabs.io/v1/text-to-speech/{0}";
        const string TTS_API_LATENCY_OPTIMIZED = "https://api.elevenlabs.io/v1/text-to-speech/{0}?optimize_streaming_latency=3";
        const string TTS_STREAM_API = "https://api.elevenlabs.io/v1/text-to-speech/{0}/stream";
        const string MODEL_TURBO = "eleven_turbo_v2";
        const string MODEL_NORMAL = "eleven_monolingual_v1";
        const string MODEL_BEST = "eleven_multilingual_v2";
        const string CHOSEN_MODEL = MODEL_BEST;
        const float STABILITY = 0.33f;
        const float SIMILARITY = 0.66f;
        const float STYLE = 0.75f;

        record VoiceSettings(
            float Stability = STABILITY, 
            float Similarity_boost = SIMILARITY,
            float Style = STYLE,
            bool Use_speaker_boost = false
        );
        record PostTtsRequest(string Text, VoiceSettings Voice_settings, string Model_id);

        private static void Log(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [PlayTTS] {message}");
        }
        public static HttpRequestMessage BuildTtsRequest(string tts, VoiceProfile profile)
        {
            var url = string.Format(TTS_API_LATENCY_OPTIMIZED, profile.Voice.VoiceId);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var json = new PostTtsRequest(
                Text: tts,
                Voice_settings: new VoiceSettings(
                    Stability: profile.Stability,
                    Similarity_boost: profile.Similarity,
                    Style: profile.Style,
                    Use_speaker_boost: false
                    ),
                Model_id: CHOSEN_MODEL
                );
            request.Content = JsonContent.Create<PostTtsRequest>(json);

            return request;
        }

        public static string CleanStringForTts(string tts)
        {
            var cleanedString = tts;
            cleanedString = Server.WEBSITE_REGEX.Replace(cleanedString, "").Trim();
            cleanedString = Server.EMOTE_REGEX.Replace(cleanedString, "").Trim();
            cleanedString = cleanedString.Replace("*", "").Trim();
            return cleanedString;
        }

        public static void PlayResponseStream(Stream responseStream)
        {
            int messageId = counter++;
            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                using (Stream ms = new MemoryStream())
                {
  
                    byte[] buffer = new byte[32768];
                    int read;
                    while ((read = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }

                    ms.Position = 0;

                    using Mp3FileReader reader = new(ms);
                    using WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
                    using Stream outStream = new MemoryStream();

                    WaveFileWriter.WriteWavFileToStream(outStream, pcmStream);
                    SoundPlayer soundPlayer = new(outStream);
                    if (soundPlayer.Stream != null)
                    {
                        soundPlayer.Stream.Position = 0;
                    }
                    timer.Stop();
                    Log($"[MSG-{messageId}] mp3 decode: {timer.ElapsedMilliseconds}ms");
                    timer.Restart();
                    soundPlayer.PlaySync();
                    timer.Stop();
                    Log($"[MSG-{messageId}] mp3 length: {timer.ElapsedMilliseconds}ms");

                }
            }
            catch (Exception e)
            {
                Log($"[MSG-{messageId}] Caught exception trying to decode+play audio stream: {e.Message}");
            }
        }
    }
}
