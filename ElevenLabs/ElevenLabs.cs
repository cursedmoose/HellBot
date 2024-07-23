using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using TwitchBot.OBS.Scene;
using static TwitchBot.Config.ElevenLabsConfig;

namespace TwitchBot.ElevenLabs
{
    public class ElevenLabs
    {
        public const string TTS_API = "https://api.elevenlabs.io/v1/text-to-speech/{0}";
        public const string TTS_API_LATENCY_OPTIMIZED = "https://api.elevenlabs.io/v1/text-to-speech/{0}?optimize_streaming_latency=3";
        public const string TTS_STREAM_API = "https://api.elevenlabs.io/v1/text-to-speech/{0}/stream";
        public const string MODEL_TURBO = "eleven_turbo_v2";
        public const string MODEL_NORMAL = "eleven_monolingual_v1";
        public const string MODEL_BEST = "eleven_multilingual_v2";

        public static readonly Dictionary<string, string> KnownModels = new()
        {
            { "best", MODEL_BEST },
            { "normal", MODEL_NORMAL },
            { "fastest", MODEL_TURBO },
            { "eleven_turbo_v2", "eleven_turbo_v2" },
            { "eleven_monolingual_v1", "eleven_monolingual_v1" },
            { "eleven_multilingual_v2", "eleven_multilingual_v2" }
        };

        record VoiceSettings(
            float Stability = 0.33f,
            float Similarity_boost = 0.66f,
            float Style = 0.75f,
            bool Use_speaker_boost = false
        );
        record PostTtsRequest(string Text, VoiceSettings Voice_settings, string Model_id);

        readonly string CHOSEN_MODEL = MODEL_BEST;
        readonly string CHOSEN_API = TTS_API_LATENCY_OPTIMIZED;
        public string API_MODEL { get; private set; } = MODEL_BEST;
        public string STREAM_MODEL { get; private set; } = MODEL_TURBO;

        public bool ShouldRemoveStartPattern = true;
        public readonly Regex START_PATTERN_REGEX = new(@"^[\w\s'-]+\:");
        public readonly Regex WHITESPACE_REGEX = new(@"\s{2,}");
        private List<string> previousSentences = new();

        readonly HttpClient client;
        public readonly long charactersStartedAt;
        public static long CharactersUsed { get; private set; } = 0;
        public readonly bool Enabled = true;
        readonly Logger log = new("ElevenLabs");

        public ElevenLabs(bool enabled = true)
        {
            Enabled = enabled;
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("accept", "audio/mpeg");
            client.DefaultRequestHeaders.Add("xi-api-key", API_KEY);

            charactersStartedAt = GetUserSubscriptionInfo().character_count;
        }

        public void PlayTts(string ttsMessage, VoiceProfile? voiceProfile)
        {
            log.Info($"Playing {ttsMessage} in the style of {voiceProfile?.Voice.VoiceName}");
            var cleanedMessage = CleanStringForTts(ttsMessage);

            if (!Enabled || voiceProfile == null || string.IsNullOrWhiteSpace(cleanedMessage))
            {
                log.Info("Could not play TTS because:");
                log.Info($"\tEnabled: {Enabled}:");
                log.Info($"\tvoiceProfile: {voiceProfile}:");
                log.Info($"\tcleanedMessage: {cleanedMessage}:");

                return;
            }

            try
            {
                var elevenLabsResponse = MakeTtsRequest(cleanedMessage, voiceProfile);
                if (elevenLabsResponse.IsSuccessStatusCode)
                {
                    using Stream responseStream = elevenLabsResponse.Content.ReadAsStream();
                    TtsPlayer.PlayResponseStream(responseStream);
                }
                else
                {
                    log.Error($"{elevenLabsResponse.StatusCode} Error when calling API.");
                }
            }
            catch (Exception e)
            {
                log.Info($"Exception trying to play TTS: {e.Message}");
            }
        }

        public HttpResponseMessage MakeTtsRequest(string ttsMessage, VoiceProfile voiceProfile)
        {
            var ttsRequest = BuildTtsRequest(CHOSEN_API, CHOSEN_MODEL, ttsMessage, voiceProfile);
            Stopwatch timer;
            log.Info($"Tts Request Initiated. Message: {ttsMessage}");
            timer = Stopwatch.StartNew();
            var response = client.Send(ttsRequest);
            timer.Stop();
            log.Info($"ElevenLabs API call: {timer.ElapsedMilliseconds}ms");
            CharactersUsed += ttsMessage.Length;
            return response;
        }


        private HttpRequestMessage BuildTtsRequest(string api, string model, string tts, VoiceProfile profile)
        {
            var url = string.Format(api, profile.Voice.VoiceId);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var json = new PostTtsRequest(
                Text: tts,
                Voice_settings: new VoiceSettings(
                    Stability: profile.Stability,
                    Similarity_boost: profile.Similarity,
                    Style: profile.Style,
                    Use_speaker_boost: false
                    ),
                Model_id: model
                );
            request.Content = JsonContent.Create<PostTtsRequest>(json);

            return request;
        }

        public string CleanStringForTts(string tts)
        {
            var cleanedString = tts;
            cleanedString = Server.WEBSITE_REGEX.Replace(cleanedString, "").Trim();
            cleanedString = Server.EMOTE_REGEX.Replace(cleanedString, "").Trim();
            cleanedString = cleanedString.Replace("cursed99", "").Trim();
            cleanedString = cleanedString.Replace("*", "").Trim();
            cleanedString = WHITESPACE_REGEX.Replace(cleanedString, "").Trim();
            return cleanedString;
        }

        public SubscriptionInfoResponse GetUserSubscriptionInfo()
        {
            return SubscriptionInfo.call(client);
        }

        private async void RunTtsStreamTask(VoiceProfile profile, string tts, ObsSceneId? obs)
        {
            var program_arguments = string.Join(" ", "/C python ElevenLabs/labs.py", API_KEY, profile.Voice.VoiceId, MODEL_TURBO);
            var tts_arguments = buildStreamArgs(tts);
            if (tts_arguments.Length <= 0) 
            {
                log.Info("No TTS args were left after cleaning.");
            }

            log.Info($"[{profile.Voice.VoiceName}]: {tts_arguments}");
            var all_arguments = string.Join(" ", program_arguments, tts_arguments);

            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = all_arguments;
            process.StartInfo.CreateNoWindow = false;

            await Server.Instance.Assistant.WaitForSilence();
            lock (Assistant.Assistant.TtsLock)
            {
                obs?.Enable();
                process.Start();
                process.WaitForExit();
                obs?.Disable();
            }
        }

        private string buildStreamArgs(string inputString)
        {
            string[] sentences = Regex.Split(inputString, @"(?<=[\.!\?])\s+");
            List<string> sentence_arguments = new List<string>();

            foreach (string sentence in sentences)
            {
                var cleanSentence = sentence;

                cleanSentence = cleanSentence
                    .Replace("\n", " ")
                    .Replace("\"", "\\\"")
                    .Replace("|", "I");

                if (ShouldRemoveStartPattern)
                {
                    cleanSentence = START_PATTERN_REGEX.Replace(cleanSentence, "").Trim();
                }

                cleanSentence = WHITESPACE_REGEX.Replace(cleanSentence, " ").Trim();
                if (cleanSentence.Length > 1)
                {
                    var sentence_arg = string.Join("", "\"", cleanSentence, "\"");
                    if (previousSentences.Contains(sentence_arg))
                    {
                        log.Debug("Found duplicate sentence arg. Should I Remove??");
                    }

                    sentence_arguments.Add(sentence_arg);
                }
            }

            previousSentences = sentence_arguments;

            return string.Join(" ", sentence_arguments.ToArray());
        }

        public void StreamTts(VoiceProfile profile, string tts, ObsSceneId? obs = null)
        {
            Task.Run(() => RunTtsStreamTask(profile, tts, obs));
        }

        public void ChangeApiModel(string model)
        {
            try
            {
                API_MODEL = KnownModels[model];
            }
            catch
            {
                log.Error($"Unknown model {model}");
            }
        }

        public void ChangeStreamModel(string model)
        {
            try
            {
                STREAM_MODEL = KnownModels[model];
            }
            catch
            {
                log.Error($"Unknown model {model}");
            }
        }

    }
}
