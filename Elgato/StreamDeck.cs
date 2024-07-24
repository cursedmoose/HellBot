using StreamDeckKeyEventArgs = OpenMacroBoard.SDK.KeyEventArgs;
using StreamDeckSdk = StreamDeckSharp.StreamDeck;

namespace TwitchBot.Elgato
{
    internal static class StreamDeckSdkExtensions
    {
        public static bool IsPressed(this StreamDeckKeyEventArgs args, int key)
        {
            return args.Key == key && args.IsDown;
        }
    }

    public class StreamDeck
    {
        readonly Logger log = new("StreamDeck");

        bool Enabled = false;
        readonly List<int> StreamDeckIndexesUsed = new List<int>() { 13, 14, 15, 21, 22, 23, 29, 30, 31 };
        Dictionary<int, StreamDeckKey> SteamDeckActions = new()
        {
            { 13, StreamDeckKey.ReadScreenRegion1 },
            { 14, StreamDeckKey.ReadScreenRegion2 },
            { 15, StreamDeckKey.ReadScreenRegion3 },
            { 29, StreamDeckKey.Debug },
            { 31, StreamDeckKey.RequestNarration }
        };

        public StreamDeck(bool enabled = true)
        {
            Enabled = enabled;
            var board = StreamDeckSdk.OpenDevice();
            board.KeyStateChanged += HandleKey;

            foreach(var action in SteamDeckActions)
            {
                board.SetKeyBitmap(action.Key, action.Value.Icon); 
            }

            log.Info("Listening to Stream Deck Events");
        }

        private async void HandleKey(object? sender, StreamDeckKeyEventArgs args)
        {
            if (!Enabled) { return; }

            log.Debug($"Received event: {args.Key} : {args.IsDown}");
            if (SteamDeckActions.ContainsKey(args.Key) && args.IsPressed(args.Key))
            {
                await SteamDeckActions[args.Key].Action();
                return;
            }
            log.Debug($"Unhandled event {args.Key}:{args.IsDown}");
        }
    }
}
