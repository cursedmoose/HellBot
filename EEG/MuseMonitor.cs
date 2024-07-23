using Rug.Osc;
using System.Net;

namespace TwitchBot.EEG
{
    public class MuseMonitor
    {
        const long TICKS_PER_SECOND = 10_000_000L;

        private readonly static Logger log = new("Muse");
        private readonly OscReceiver Receiver;
        private readonly Task MonitorTask;
        private readonly IPAddress IP_Address = IPAddress.Parse("192.168.1.197");
        private readonly int Port = 5000;
        private readonly long ProfilingLength = TICKS_PER_SECOND * 20; // 60 seconds
        private bool Profiling = false;
        private long ProfileTime = 0;
        private Dictionary<string, List<MindMonitorPacket>> Profiler = new();
        private Dictionary<string, MindMonitorPacket> LastReadings = new();
        private Dictionary<string, MindMonitorProfilerResult> LastMinuteAverages = new();


        private static readonly string GAMMA = "/muse/elements/gamma_absolute"; // heightened perception, peak state
        private static readonly string BETA = "/muse/elements/beta_absolute"; // decision making, task solving, learning
        private static readonly string ALPHA = "/muse/elements/alpha_absolute"; // relaxation, creativity
        private static readonly string THETA = "/muse/elements/theta_absolute"; // detached dreaming, autopilot, repetitive tasks
        private static readonly string DELTA = "/muse/elements/delta_absolute"; // active dreaming, loss of awareness


        public MuseMonitor(bool Enabled = true)
        {
            Receiver = new(IP_Address, Port);
            MonitorTask = new(ListenLoop);
            if (!Enabled) { return; }

            Start();
        }

        public void Start()
        {
            Receiver.Connect();
            MonitorTask.Start();
        }

        public void Stop()
        {
            Receiver.Close();
            MonitorTask.Dispose();
        }

        private void ListenLoop()
        {
            log.Info("Starting EEG Listener..");
            try
            {
                while (Receiver.State != OscSocketState.Closed)
                {
                    // if we are in a state to recieve
                    if (Receiver.State == OscSocketState.Connected)
                    {
                        OscPacket packet;
                        if (Receiver.TryReceive(out packet))
                        {
                            var info = packet.ToString();
                            var info2 = (OscBundle)packet;
                            var data = Parse(packet);

                            if (!LastReadings.ContainsKey(data.Path))
                            {
                                LastReadings.Add(data.Path, data);
                            } 
                            else
                            {
                                LastReadings[data.Path] = data;
                            }

                            if (data.Path.Contains("blink"))
                            {
                                //log.Info($"Blinked at {data.TimeStamp}");
                            } 
                            else if (data.Path.Contains("jaw_clench"))
                            {
                                //log.Info($"Clenched at {data.TimeStamp}");
                            }

                            if (Profiling)
                            {
                                Profile(data);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // if the socket was connected when this happens
                // then tell the user
                if (Receiver.State == OscSocketState.Connected)
                {
                    Console.WriteLine("Exception in listen loop");
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public void StartProfiling()
        {
            ProfileTime = DateTime.UtcNow.Ticks;
            Profiler.Clear();
            Profiling = true;
            log.Info("Profiling started.");
        }

        public void StopProfiling()
        {
            Profiling = false;
            log.Info("Profiling stopped.");
            GetProfilerResults();
            Profiler.Clear();
        }

        public void DumpProfile(string filename)
        {
            
        }

        private void Profile(MindMonitorPacket Packet)
        {
            if (DateTime.UtcNow.Ticks <= (ProfileTime + ProfilingLength))
            {
                if (Profiler.ContainsKey(Packet.Path))
                {
                    Profiler[Packet.Path].Add(Packet);
                }
                else
                {
                    Profiler.Add(Packet.Path, new() { Packet });
                }

            }
            else
            {
                StopProfiling();
            }
        }

        private void GetProfilerResults()
        {
            LastMinuteAverages.Clear();
            foreach (var kvp in Profiler)
            {
                List<float> avgs = new();
                float min = 0;
                float max = 0;

                if (kvp.Value.First().Path.Contains("blink") || kvp.Value.First().Path.Contains("jaw_clench"))
                {
                    int numSamples = kvp.Value.Count;
                    avgs.Add(numSamples / 60f);
                    min = 0;
                    max = 1;
                }
                else
                {
                    for (int x = 0; x < kvp.Value.First().Args.Count; x++)
                    {
                        float avg = kvp.Value.Average(it => it.Args[x]);
                        avgs.Add(avg);

                        min = kvp.Value.Min(it => it.Args[x]);
                        max = kvp.Value.Max(it => it.Args[x]);

                    }
                }

                MindMonitorPacket averages = new(kvp.Value.First().TimeStamp, kvp.Value.First().Path, avgs);
                MindMonitorProfilerResult result = new(
                    Path: kvp.Key,
                    Count: kvp.Value.Count,
                    Min: min,
                    Max: max,
                    Averages: avgs
                    );

                LastMinuteAverages.Add(averages.Path, result);
                log.Info($"{averages.Path}: [{string.Join(',', averages.Args)}]");
            }
        }

        private MindMonitorPacket Parse(OscPacket packet)
        {
            var data = packet.ToString();
            var dataChunks = data?.Split(",", 3);

            if (dataChunks?.Length != 3) {
                log.Error($"Could not parse {data} as we received {dataChunks?.Length} args");
                return new MindMonitorPacket("oh", "no", new List<float> { 0f });
            } 
            else
            {
                var timestamp = dataChunks[1];
                var packetData = dataChunks[2].Trim(new char[] { ' ', '{', '}' }).Split(',');
                var path = packetData[0];
                float maybeArg = 0f;
                List<float> maybeArgs = new();
                for (int x = 1; x < packetData.Length; x++)
                {
                    var arg = float.TryParse(packetData[x].Trim('f'), out maybeArg);
                    if (arg)
                    {
                        maybeArgs.Add(maybeArg);
                    }
                    else
                    {
                        log.Error("oh no");
                    }
                }

                //var args = float.TryParse(packetData[1], out maybeArg);

                return new MindMonitorPacket(
                    TimeStamp: timestamp,
                    Path: path,
                    Args: maybeArgs
               );
            }
        }
    }

    public record MindMonitorPacket(
        string TimeStamp,
        string Path,
        List<float> Args
     );

    public record MindMonitorProfilerResult(
        string Path,
        int Count,
        float Min,
        float Max,
        List<float> Averages
    );
}
