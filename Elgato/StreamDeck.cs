using OpenMacroBoard.SDK;
using StreamDeckSharp;
using IDeviceContext = OpenMacroBoard.SDK.IDeviceContext;
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
        IDeviceContext Deck;
        public readonly List<int> StreamDeckIndexesUsed = new List<int>() { 13, 14, 15, 21, 22, 23, 29, 30, 31 };
        public StreamDeck(bool enabled = true)
        {
            Enabled = enabled;
            Deck = DeviceContext.Create().AddListener<StreamDeckListener>();
            var board = StreamDeckSdk.OpenDevice();
            board.KeyStateChanged += HandleKey;

            var defaultIcon = KeyBitmap.Create.FromFile("Elgato/Icons/sweet.png");

            foreach(var index in StreamDeckIndexesUsed)
            {
                board.SetKeyBitmap(index, defaultIcon);
            }

            log.Info("Listening to Stream Deck Events");
        }

        private async void HandleKey(object? sender, StreamDeckKeyEventArgs args)
        {
            if (!Enabled) { return; }

            log.Info($"Received event: {args.Key} : {args.IsDown}");
            if (args.IsPressed(31))
            {
                await Server.Instance.Narrator.ReactToCurrentScreen();
            }

        }
    }
}
