using Websocket.Client;
using System.Net.WebSockets;
using System.Text.Json;
using NAudio.Wave;
using System.Media;
using System.Diagnostics;
using static TwitchBot.Config.ElevenLabsConfig;


namespace TwitchBot.ElevenLabs
{
    public class TtsWebsocket
    {
        private WebsocketClient client;
        readonly string WebsocketEndpoint = "wss://api.elevenlabs.io/v1/text-to-speech/{0}/stream-input?model_id={1}";
        readonly Logger log = new("ElevenLabsWebsocket");

        readonly AudioStreamRequest Begin = new(xi_api_key: API_KEY, voice_settings: new(0.8f, 0.8f, 0.5f));
        readonly AudioStreamInput End = new("");
        readonly string BeginRequest;
        readonly string EndRequest;
        List<byte[]> buffer;
        private readonly object bufferLock = new object();
        bool isPlaying = false;
        Stopwatch timeToFirstRead = new();
        Task playTask;
        public TtsWebsocket(VoiceProfile voice, string model = ElevenLabs.MODEL_BEST)
        {
            Begin = new(xi_api_key: API_KEY, voice_settings: new(stability: voice.Stability, similarity_boost: voice.Similarity, style: voice.Style));
            BeginRequest = JsonSerializer.Serialize(Begin);
            EndRequest = JsonSerializer.Serialize(End);
            var exitEvent = new ManualResetEvent(false);
            buffer = new();

            var url = new Uri(string.Format(WebsocketEndpoint, voice.Voice.VoiceId, model));
            client = new WebsocketClient(url);
            Initialize();
        }

        private void Initialize()
        {
            client.ReconnectTimeout = TimeSpan.FromSeconds(30);
            client.ReconnectionHappened.Subscribe(info => Websocket_OnReconnect(info));
            client.MessageReceived.Subscribe(message => Websocket_OnMessageReceived(message));
            client.DisconnectionHappened.Subscribe(info => Websocket_OnDisconnect(info));

            playTask = Task.Run(Play_Buffer);
        }

        private void Websocket_OnReconnect(ReconnectionInfo info)
        {
            //log.Info($"Reconnected due to {info.Type}");
        }

        private void Websocket_OnDisconnect(DisconnectionInfo info)
        {
            //log.Info($"Disconnected due to {info.CloseStatusDescription}");
        }

        private void Websocket_OnMessageReceived(ResponseMessage message)
        {
            // log.Info($"Received Message");
            if (message.Text != null) {
                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<AudioStreamResponse>(message.Text);
                    if (jsonResponse?.isFinal != true)
                    {
                        var timer = new Stopwatch();
                        timer.Start();
                        var byteData = Convert.FromBase64String(jsonResponse?.audio);
                        timer.Stop();

                        lock (bufferLock)
                        {
                            buffer.Add(byteData);
                        }
                    }
                    
                    if ((jsonResponse?.isFinal == true || buffer.Count > 3) && !isPlaying)
                    {
                        log.Info("pressing play ooo");
                        //Task.Run(Play_Buffer);

                        //Play_Buffer();
                    }
                }
                catch (Exception ex)
                {
                    
                }
            }
        }

        private void Play_Buffer()
        {
            do
            {
                if (buffer.Count > 0)
                {
                    byte[] bigBuffer;
                    lock (bufferLock)
                    {
                        bigBuffer = buffer.SelectMany(i => i).ToArray();
                        buffer = new();
                    }
                    //await Task.Run(Play_AudioSnippet);
                    lock (Assistant.Assistant.TtsLock)
                    {
                        Server.Instance.Narrator.Obs.Enable();
                        Play_AudioSnippet(bigBuffer);
                        if (!isPlaying && buffer.Count == 0)
                        {
                            Server.Instance.Narrator.Obs.Disable();
                        }
                    }
                }
                Task.Delay(100);
            } while (true);
        }

        private void Play_AudioSnippet(byte[] audioSnippet)
        {
            var timer = Stopwatch.StartNew();
            using MemoryStream ms = new(audioSnippet);
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
            log.Info($"mp3 decode: {timer.ElapsedMilliseconds}ms");
            timer.Restart();
            if (timeToFirstRead.IsRunning)
            {
                timeToFirstRead.Stop();
                log.Info($"time to first Read: {timeToFirstRead.ElapsedMilliseconds}ms");
            }
            isPlaying = true;
            soundPlayer.PlaySync();
            isPlaying = false;
            timer.Stop();
            log.Info($"mp3 length: {timer.ElapsedMilliseconds}ms");
            return;
        }

        private void Websocket_SendTextSnippet(string snippet)
        {
            log.Info($"Asking for {snippet}");
            AudioStreamInput input = new(snippet.Trim() + " ");
            var jsonInput = JsonSerializer.Serialize(input);
            client.Send(jsonInput);
        }

        public bool isRunning()
        {
            return client.IsRunning;
        }

        public async void Start()
        {
            if (playTask.Status == TaskStatus.Running || playTask.Status == TaskStatus.Faulted) {
                // Task die?
                playTask = Task.Run(Play_Buffer);
            }

            await client.Start();
            client.Send(BeginRequest);
        }

        public async void Stop()
        {
            await client.Stop(WebSocketCloseStatus.NormalClosure, "goodbye");
        }

        public void Send(string message)
        {
            timeToFirstRead.Restart();
            client.Send(BeginRequest);
            var messageTokens = message.Split(' ');
            var messageTokens2 = messageTokens.Chunk(5);

            foreach (var tokens in messageTokens2)
            {
                Websocket_SendTextSnippet(string.Join(" ", tokens));
            }
            client.Send(EndRequest);
        }

    }

    internal record AudioStreamRequest(
        string xi_api_key,
        VoiceSettings voice_settings,
        string text = " "
    );

    internal record AudioStreamInput(
        string text
    );

    internal record VoiceSettings(
        float stability,
        float similarity_boost,
        float style
        );

    internal record AudioStreamResponse(
        string? audio,
        bool? isFinal,
        Alignment? normalizedAlignment,
        Alignment? alignment
    );

    internal record Alignment(
        string char_start_times_ms,
        string chars_durations_ms,
        string[] chars
        );
}
