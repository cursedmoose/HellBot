namespace TwitchBot.SpeechToText
{
    public class MicrophoneListener
    {
        public static bool Enabled { get; private set; }
        private readonly Logger log = new("Microphone");

        public MicrophoneListener(bool enabled = false) 
        {
            Enabled = enabled;
            if (!Enabled) { return; }

            var waveIn = new NAudio.Wave.WaveInEvent
            {
                DeviceNumber = 0, // indicates which microphone to use
                WaveFormat = new NAudio.Wave.WaveFormat(rate: 8000, bits: 16, channels: 1),
                BufferMilliseconds = 500
            };
            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.StartRecording();
        }

        public void WaveIn_DataAvailable(object? sender, NAudio.Wave.WaveInEventArgs e)
        {
            // copy buffer into an array of integers
            Int16[] values = new Int16[e.Buffer.Length / 2];
            Buffer.BlockCopy(e.Buffer, 0, values, 0, e.Buffer.Length);

            // determine the highest value as a fraction of the maximum possible value
            float fraction = (float)values.Max() / 32768;

            // print a level meter using the console
            string bar = new('#', (int)(fraction * 70));
            string meter = "[" + bar.PadRight(60, '-') + "]";
            Console.CursorLeft = 0;
            Console.CursorVisible = false;
            Console.Write($"{meter} {fraction * 100:00.0}%");

            if (fraction * 100 > 50)
            {
                log.Info($"{meter} {fraction * 100:00.0}%");
            }
    }


}
}
