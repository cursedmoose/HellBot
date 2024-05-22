
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Tobii.Research;

namespace TwitchBot.EyeTracking
{
    public class TobiiEyeTracker
    {
        private readonly static Logger log = new("Tobii");
        private IEyeTracker? EyeTracker;

        public TobiiEyeTracker(bool Enabled)
        {
            if (!Enabled) { return; }

            var EyeTrackers = EyeTrackingOperations.FindAllEyeTrackers();
            
            if (!EyeTrackers.Any())
            {
                log.Error("No eye trackers were found! Cannot load eye tracker.");
                return;
            }

            EyeTracker = EyeTrackers[0];
            log.Info($"Loaded Tracker {EyeTracker.DeviceName}: {EyeTracker.SerialNumber}: {EyeTracker.Address}");
            var freq = EyeTracker.GetGazeOutputFrequency();

            var area = EyeTracker.GetDisplayArea();
            EyeTracker.SetDisplayArea(new(new(0, 0, 0), new(1, 1, 0), new(1, 1, 1)));
            var mode = EyeTracker.GetEyeTrackingMode();
            var modes = EyeTracker.GetAllEyeTrackingModes();
            var freqs = EyeTracker.GetAllGazeOutputFrequencies();
            foreach (var m in freqs)
            {
                log.Info($"Available Mode: {m}");
            }
            EyeTracker.GazeDataReceived += EyeTracker_OnGazeDataReceived;
            log.Info($"Subscribed to Gaze Data. {mode} | {freq}");
            log.Info($"Area is ({area.TopLeft.X},{area.TopLeft.Y})x({area.BottomRight.X}, {area.BottomRight.Y})");
            log.Info($"WxH {area.Width}mm x {area.Height}mm");

            PrintCapabilities(EyeTracker);
            EyeTrackingOperations.LogReceived += EyeTracker_LogReceived;
            EyeTracker.ConnectionLost += EyeTracker_LogEvent;
        
        }

        private void PrintCapabilities(IEyeTracker tracker)
        {
            var capabilities = tracker.DeviceCapabilities;
            Console.WriteLine(" CanDoHMDBasedCalibration: {0}", capabilities.HasFlag(Capabilities.CanDoHMDBasedCalibration));
            Console.WriteLine(" CanDoScreenBasedCalibration: {0}", capabilities.HasFlag(Capabilities.CanDoScreenBasedCalibration));
            Console.WriteLine(" CanSetDisplayArea: {0}", capabilities.HasFlag(Capabilities.CanSetDisplayArea));
            Console.WriteLine(" HasExternalSignal: {0}", capabilities.HasFlag(Capabilities.HasExternalSignal));
            Console.WriteLine(" HasEyeImages: {0}", capabilities.HasFlag(Capabilities.HasEyeImages));
            Console.WriteLine(" HasGazeData: {0}", capabilities.HasFlag(Capabilities.HasGazeData));
            Console.WriteLine(" HasHMDGazeData: {0}", capabilities.HasFlag(Capabilities.HasHMDGazeData));
            Console.WriteLine(" HasHMDLensConfig: {0}", capabilities.HasFlag(Capabilities.HasHMDLensConfig));
            Console.WriteLine(" CanDoMonocularCalibration: {0}", capabilities.HasFlag(Capabilities.CanDoMonocularCalibration));
        }

        private void EyeTracker_LogEvent(object? Sender, EventArgs e)
        {
            log.Info(e.ToString());
        }

        private static void EyeTracker_LogReceived(object sender, LogEventArgs e)
        {
            Console.WriteLine("\nSource: {0}\nLevel: {1}\nMessage: \"{2}\" Time Stamp: \"{3}\"", e.Source, e.Level, e.Message, e.SystemTimeStamp);
        }

        private void EyeTracker_OnGazeDataReceived(object? Sender, GazeDataEventArgs e)
        {
            Console.WriteLine("WORKING");
            log.Info($"Left: {e.LeftEye.GazePoint} | Right: {e.RightEye.GazePoint}");

            Console.WriteLine(
                "Got gaze data with {0} left eye origin at point ({1}, {2}, {3}) in the user coordinate system.",
                e.LeftEye.GazeOrigin.Validity,
                e.LeftEye.GazeOrigin.PositionInUserCoordinates.X,
                e.LeftEye.GazeOrigin.PositionInUserCoordinates.Y,
                e.LeftEye.GazeOrigin.PositionInUserCoordinates.Z);
            Console.WriteLine(
                "Got gaze data with {0} right eye origin at point ({1}, {2}, {3}) in the user coordinate system.",
                e.RightEye.GazeOrigin.Validity,
                e.RightEye.GazeOrigin.PositionInUserCoordinates.X,
                e.RightEye.GazeOrigin.PositionInUserCoordinates.Y,
                e.RightEye.GazeOrigin.PositionInUserCoordinates.Z);
        }

    }
}
