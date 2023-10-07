using NAudio.Wave;
using System.Diagnostics;
using System.Media;
using System.Net.Http.Json;

namespace TwitchBot.ElevenLabs
{
    internal class PlayTts
    {
        static int counter;

        const String TTS_API = "https://api.elevenlabs.io/v1/text-to-speech/{0}";
        const String TTS_API_LATENCY_OPTIMIZED = "https://api.elevenlabs.io/v1/text-to-speech/{0}?optimize_streaming_latency=3";
        const float STABILITY = 0.33f;
        const float SIMILARITY = 0.66f;

        record VoiceSettings(float stability = STABILITY, float similarity_boost = SIMILARITY);
        record PostTtsRequest(string text, VoiceSettings voice_settings);

        private static void Log(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [PlayTTS] {message}");
        }
        public static HttpRequestMessage buildTtsRequest(string tts, VoiceProfile profile)
        {
            var url = string.Format(TTS_API_LATENCY_OPTIMIZED, profile.voiceId);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var json = new PostTtsRequest(text: tts, voice_settings: new VoiceSettings(profile.stability, profile.similarity));
            request.Content = JsonContent.Create<PostTtsRequest>(json);

            return request;
        }

        public static string cleanStringForTts(string tts)
        {
            var cleanedString = tts;
            cleanedString = Server.WEBSITE_REGEX.Replace(cleanedString, "").Trim();
            cleanedString = Server.EMOTE_REGEX.Replace(cleanedString, "").Trim();
            cleanedString = cleanedString.Replace("*", "").Trim();
            return cleanedString;
        }

        public async static void Play(string ttsMessage, VoiceProfile voiceProfile)
        {
            var cleanedMessage = cleanStringForTts(ttsMessage);

            if (voiceProfile == null || cleanedMessage.Length == 0)
            {
                return;
            }
            int messageId = counter++;
            Stopwatch timer;
            Log($"[MSG-{messageId}] Initiated. Message: {cleanedMessage}");

            var client = new HttpClient();

            client.DefaultRequestHeaders.Add("accept", "audio/mpeg");
            client.DefaultRequestHeaders.Add("xi-api-key", ElevenLabs.API_KEY);
            var ttsRequest = buildTtsRequest(cleanedMessage, voiceProfile);
            timer = Stopwatch.StartNew();
            var response2 = client.Send(ttsRequest);
            timer.Stop();
            Log($"[MSG-{messageId}] API call: {timer.ElapsedMilliseconds}ms");

            if (!response2.IsSuccessStatusCode)
            {
                Log($"[MSG-{messageId}] Error: API returned {response2.StatusCode}");
                return;
            }

            try
            {
                timer.Restart();
                using (Stream ms = new MemoryStream())
                {
                    using (Stream stream = response2.Content.ReadAsStream())
                    {
                        byte[] buffer = new byte[32768];
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                    }

                    ms.Position = 0;

                    using (Mp3FileReader reader = new Mp3FileReader(ms))
                    {
                        using (WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(reader))
                        {
                            using (Stream outStream = new MemoryStream())
                            {
                                // WaveFileWriter.CreateWaveFile("test.wav", pcmStream);
                                WaveFileWriter.WriteWavFileToStream(outStream, pcmStream);
                                SoundPlayer soundPlayer = new SoundPlayer(outStream);
                                soundPlayer.Stream.Position = 0;
                                soundPlayer.PlaySync();
                            }
                        }
                    }
                }
                timer.Stop();
                Log($"[MSG-{messageId}] mp3 decode: {timer.ElapsedMilliseconds}ms");
            }
            catch (Exception e)
            {
                Log($"[MSG-{messageId}] Caught exception trying to decode+play audio stream: {e.Message}");
            }
        }
    }
}
