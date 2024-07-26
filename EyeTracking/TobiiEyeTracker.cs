using Tobii.GameIntegration.Net;
using TwitchLib.Api.ThirdParty.ModLookup;

namespace TwitchBot.EyeTracking
{
    public class TobiiEyeTracker
    {
        private readonly static Logger log = new("Tobii");

        public TobiiEyeTracker(bool Enabled)
        {
            if (!Enabled) { return; }
            TobiiGameIntegrationApi.PrelinkAll();
            TobiiGameIntegrationApi.SetApplicationName("Hellbot");
            TobiiGameIntegrationApi.TrackRectangle(new() { Left = 0, Top = 0, Right = 2560, Bottom = 1440 });
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

            Task.Run(async () =>
            {
                do
                {
                    TobiiGameIntegrationApi.Update();
                    LatestGazePoint();
                    await Task.Delay(1000);
                } while (true);
            });
        }

        public void LatestGazePoint()
        {
            var d = TobiiGameIntegrationApi.TryGetLatestGazePoint(out var gazePoint);
            var probablyX = (2560 / 2) * gazePoint.X + (2560 / 2);
            var probablyY = (1440 / 2) - ((1440 / 2) * gazePoint.Y);
            log.Info($"Gaze Point: {probablyX}:{probablyY} ");
        }
    }
}
