using OpenMacroBoard.SDK;
using TwitchBot.Hotkeys;

namespace TwitchBot.Elgato
{
    internal record StreamDeckKey(
        string Name,
        Func<Task> Action,
        KeyBitmap Icon
    )
    {
        internal static StreamDeckKey Debug = new(
            Name: "DebugKey",
            Action: async () => Console.WriteLine("Hello from StreamDeck Debug!"),
            Icon: KeyBitmap.Create.FromFile("Elgato/Icons/sweet.png")
        );

        internal static StreamDeckKey RequestNarration = new(
            Name: "NarrationRequest",
            Action: async () => await Server.Instance.Narrator.ReactToCurrentScreen(),
            Icon: KeyBitmap.Create.FromFile("Elgato/Icons/really3.png")
        );

        internal static StreamDeckKey ReadScreenRegion1 = new(
            Name: "ReadScreenRegion1",
            Action: async () => await HotKeyManager.ReadScreenRegion(0),
            Icon: KeyBitmap.Create.FromRgb(255, 255, 0)
        );

        internal static StreamDeckKey ReadScreenRegion2 = new(
            Name: "ReadScreenRegion2",
            Action: async () => await HotKeyManager.ReadScreenRegion(1),
            Icon: KeyBitmap.Create.FromRgb(255, 0, 0)
        );

        internal static StreamDeckKey ReadScreenRegion3 = new(
            Name: "ReadScreenRegion3",
            Action: async () => await HotKeyManager.ReadScreenRegion(2),
            Icon: KeyBitmap.Create.FromRgb(0, 0, 255)
        );
    }
}
