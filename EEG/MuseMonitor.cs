using Rug.Osc;
using System.Net;

namespace TwitchBot.EEG
{
    public class MuseMonitor
    {
        private readonly static Logger log = new("Muse");
        private readonly OscReceiver Receiver;
        private readonly Task MonitorTask;
        private readonly Task BrainStatePoller;
        private readonly IPAddress IP_Address = IPAddress.Parse("192.168.1.197");
        private readonly int Port = 5000;

        private static bool Enabled = false;

        private readonly Dictionary<BrainWave, Queue<float>> BrainWaves = new()
        {
            { BrainWave.Alpha, new() },
            { BrainWave.Beta, new() },
            { BrainWave.Delta, new() },
            { BrainWave.Gamma, new() },
            { BrainWave.Theta, new() },
        };


        private const string ALPHA = "/muse/elements/alpha_absolute"; // relaxation, creativity
        private const string BETA = "/muse/elements/beta_absolute"; // decision making, task solving, learning
        private const string DELTA = "/muse/elements/delta_absolute"; // active dreaming, loss of awareness
        private const string GAMMA = "/muse/elements/gamma_absolute"; // heightened perception, peak state
        private const string THETA = "/muse/elements/theta_absolute"; // detached dreaming, autopilot, repetitive tasks

        private string CurrentState = "None";

        public MuseMonitor(bool enabled = true)
        {
            Enabled = enabled;
            Receiver = new(IP_Address, Port);
            MonitorTask = new(ListenLoop);
            BrainStatePoller = new(PollingLoop);
            if (!Enabled) { return; }
            Start();
        }

        public static bool IsEnabled()
        {
            return Enabled;
        }

        public Task Start()
        {
            Receiver.Connect();
            log.Info($"Receiver is {Receiver.State}");
            MonitorTask.Start();
            BrainStatePoller.Start();
            return Task.CompletedTask;
            // If this is not receiving, make sure MindMonitor is on the right network
        }

        public Task Stop()
        {
            Receiver.Close();
            try
            {
                MonitorTask.Dispose();
            }
            catch (InvalidOperationException _)
            {
                log.Info("MonitorTask not running");
            }
            try
            {
                BrainStatePoller.Dispose();
            }
            catch (InvalidOperationException _)
            {
                log.Info("BrainStatePoller not running");
            }
            return Task.CompletedTask;
        }

        private void ListenLoop()
        {
            log.Info("Starting EEG Listener..");
            try
            {
                while (Receiver.State != OscSocketState.Closed)
                {
                    if (Receiver.State == OscSocketState.Connected)
                    {
                        OscPacket packet;
                        if (Receiver.TryReceive(out packet))
                        {
                            var info = packet.ToString();
                            var info2 = (OscBundle)packet;
                            Parse(packet);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Receiver.State == OscSocketState.Connected)
                {
                    Console.WriteLine("Exception in listen loop");
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private async void PollingLoop()
        {
            log.Info("Polling started.");
            do
            {
                await Task.Delay(15_000);
                var newState = CurrentBrainWaveState();
                if (newState != CurrentState)
                {
                    log.Info($"Moving from {CurrentState} to {newState}");
                    CurrentState = newState;
                }
            } while (Receiver.State != OscSocketState.Closed);
        }

        private MindMonitorPacket Parse(OscPacket packet)
        {
            var data = packet.ToString();
            var dataChunks = data?.Split(",", 3);

            if (dataChunks?.Length != 3)
            {
                log.Error($"Could not parse {data} as we received {dataChunks?.Length} args");
                return new MindMonitorPacket("oh", "no", new List<float> { 0f });
            }
            else
            {
                var timestamp = dataChunks[1];
                var packetData = dataChunks[2].Trim(new char[] { ' ', '{', '}' }).Split(',');
                var path = packetData[0];
                List<float> maybeArgs = new();

                switch (path)
                {
                    case ALPHA:
                        ParseBrainWave(BrainWave.Alpha, packetData[1]);
                        break;
                    case BETA:
                        ParseBrainWave(BrainWave.Beta, packetData[1]);
                        break;
                    case GAMMA:
                        ParseBrainWave(BrainWave.Gamma, packetData[1]);
                        break;
                    case DELTA:
                        ParseBrainWave(BrainWave.Delta, packetData[1]);
                        break;
                    case THETA:
                        ParseBrainWave(BrainWave.Theta, packetData[1]);
                        break;
                }

                return new MindMonitorPacket(
                    TimeStamp: timestamp,
                    Path: path,
                    Args: maybeArgs
               );
            }
        }

        internal float GetCurrentAverage(BrainWave waveType)
        {
            return BrainWaves[waveType].Average();
        }

        public void PrintAverages()
        {
            foreach (var kvp in BrainWaves)
            {
                log.Info($"{kvp.Key}: {kvp.Value.Average()}");
            }
        }

        public string CurrentBrainWaveState()
        {
            if (!Enabled) { return ""; }

            float highestAverage = 0f;
            BrainWave highestState = BrainWave.None;
            foreach (var kvp in BrainWaves)
            {
                if (kvp.Value.Count > 0)
                {
                    var thisAverage = kvp.Value.Average();
                    if (thisAverage > highestAverage)
                    {
                        highestAverage = thisAverage;
                        highestState = kvp.Key;
                    }
                }
            }

            return GetWaveStateDefinition(highestState);
        }

        private bool ParseBrainWave(BrainWave waveType, string value)
        {
            if (float.TryParse(value.Trim('f'), out var maybeArg))
            {
                if (BrainWaves[waveType].Count >= 100)
                {
                    BrainWaves[waveType].Dequeue();
                }
                BrainWaves[waveType].Enqueue(maybeArg);
                return true;
            }

            return false;
        }

        private string GetWaveStateDefinition(BrainWave waveState)
        {
            if (Enabled)
            {
                switch (waveState)
                {
                    case BrainWave.Alpha:
                        return "calm";
                    case BrainWave.Beta:
                        return "alert";
                    case BrainWave.Delta:
                        return "relaxed";
                    case BrainWave.Gamma:
                        return "very focused";
                    case BrainWave.Theta:
                        return "introspective";
                }
            }

            return "";
        }
    }
}
internal record MindMonitorPacket(
    string TimeStamp,
    string Path,
    List<float> Args
 );

internal enum BrainWave
{
    None = 0,
    Alpha,
    Beta,
    Delta,
    Gamma,
    Theta
}
