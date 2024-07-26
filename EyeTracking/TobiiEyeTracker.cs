using Tobii.GameIntegration.Net;

namespace TwitchBot.EyeTracking
{
    public class TobiiEyeTracker
    {
        private readonly static Logger log = new("Tobii");
        static bool Running = false;
        private readonly static TobiiRectangle MainScreen = new() { Left = 0, Top = 0, Right = 2560, Bottom = 1440 };
        private readonly static Rectangle VisionCone = new() { X = 0, Y = 0, Width = 800, Height = 600 };
        
        static bool Enabled = false;

        public TobiiEyeTracker(bool enabled)
        {
            Enabled = enabled;
            if (!Enabled) { return; }
            TobiiGameIntegrationApi.PrelinkAll();
            TobiiGameIntegrationApi.SetApplicationName("Hellbot");
            TobiiGameIntegrationApi.TrackRectangle(MainScreen);
            var info = TobiiGameIntegrationApi.GetTrackerInfo();
            TobiiGameIntegrationApi.TrackTracker(info.Url);
            TobiiGameIntegrationApi.Update();
            TobiiGameIntegrationApi.UpdateTrackerInfos();

            var a = TobiiGameIntegrationApi.IsTrackerConnected();
            var b = TobiiGameIntegrationApi.IsApiInitialized();
            var c = TobiiGameIntegrationApi.GetTrackerInfo();
            var d = TobiiGameIntegrationApi.TryGetLatestGazePoint(out var gazePoint);

            log.Info($"Tracker Connected: {TobiiGameIntegrationApi.IsTrackerConnected()}");
            log.Info($"API Initialized: {TobiiGameIntegrationApi.IsApiInitialized()}");
            log.Info($"Tracker Info: {c.IsAttached}");
            log.Info($"Gaze Point: {gazePoint}");

            Start();
        }

        public static bool IsEnabled()
        {
            return Enabled;
        }

        public void Start()
        {
            if (!Enabled) { return; }
            Running = true;
            Task.Run(async () =>
            {
                do
                {
                    TobiiGameIntegrationApi.Update();
                    CaptureLatestVisionArea();
                    await Task.Delay(5_000);
                } while (Running);
            });
        }

        public void Stop()
        {
            Running = false;
        }

        public Point LatestGazePoint()
        {
            TobiiGameIntegrationApi.TryGetLatestGazePoint(out var gazePoint);
            var screenPixel = gazePoint.ToScreenPixel();
            log.Debug($"Gaze Point: {screenPixel.X}:{screenPixel.Y} ");
            return screenPixel;
        }

        public Rectangle GetVisionRectangle(Point gazeCenter)
        {
            var rectangleTopLeft = new Point(gazeCenter.X - (VisionCone.Width / 2), gazeCenter.Y - (VisionCone.Height / 2));

            var realTopLeftX = Math.Clamp(rectangleTopLeft.X, 0, (MainScreen.Right - VisionCone.Width));
            var realTopLeftY = Math.Clamp(rectangleTopLeft.Y, 0, (MainScreen.Bottom - VisionCone.Height));
            log.Debug($"Gaze Rectangle: {realTopLeftX}:{realTopLeftY} to {realTopLeftX + VisionCone.Width}:{realTopLeftY + VisionCone.Height} ");
            return new Rectangle(realTopLeftX, realTopLeftY, VisionCone.Width, VisionCone.Height);
        }

        public string CaptureLatestVisionArea()
        {
            var gazePoint = LatestGazePoint();
            var gazeArea = GetVisionRectangle(gazePoint);
            return Server.Instance.screen.TakeScreenRegion(gazeArea, "eyetracker");
        }
    }

    internal static class TobiiExtensions
    {
        static int ScreenX = 2560;
        static int ScreenY = 1440;
        internal static Point ToScreenPixel(this GazePoint gazePoint)
        {
            var probablyX = (int)((ScreenX / 2) * gazePoint.X + (ScreenX / 2));
            var probablyY = (int)((ScreenY / 2) - ((ScreenY / 2) * gazePoint.Y));
            var x = Math.Clamp(probablyX, 0, ScreenX);
            var y = Math.Clamp(probablyY, 0, ScreenY);

            return new() { X = probablyX, Y = probablyY };
        }
    }
}
