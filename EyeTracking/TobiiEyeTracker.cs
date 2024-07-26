namespace TwitchBot.EyeTracking
{
    public class TobiiEyeTracker
    {
        private readonly static Logger log = new("Tobii");

        public TobiiEyeTracker(bool Enabled)
        {
            if (!Enabled) { return; }
        }
    }
}
