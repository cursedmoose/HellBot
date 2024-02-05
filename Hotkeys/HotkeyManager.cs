using System.Runtime.InteropServices;
using TwitchBot.ScreenCapture;

namespace TwitchBot.Hotkeys
{
    public static class HotKeyManager
    {
        public static event EventHandler<HotKeyEventArgs> HotKeyPressed;
        public static Dictionary<HotkeyCommand, RegisteredHotkey> RegisteredHotkeys = new();
        public static readonly object RegionLock = new();
        private static Logger log = new("HotkeyManager");

        public static Hotkey CaptureNewHotKey()
        {
            var newKey = Console.ReadKey();
            log.Debug($"Pressed: {newKey.Key}+{newKey.Modifiers}");
            var convertedHotkey = new Hotkey((Keys)newKey.Key, newKey.Modifiers.toKeyModifiers());
            log.Debug($"Converted: {convertedHotkey.Keys}+{convertedHotkey.Modifiers}");

            return convertedHotkey;
        }

        private static KeyModifiers toKeyModifiers(this ConsoleModifiers modifiers)
        {
            KeyModifiers keyMods = 0;

            if (modifiers.HasFlag(ConsoleModifiers.Shift))
            {
                keyMods |= KeyModifiers.Shift;
            }
            if (modifiers.HasFlag(ConsoleModifiers.Alt))
            {
                keyMods |= KeyModifiers.Alt;
            }
            if (modifiers.HasFlag(ConsoleModifiers.Control))
            {
                keyMods |= KeyModifiers.Control;
            }


            return keyMods;
        }

        public static int RegisterHotKey(Keys key, KeyModifiers modifiers)
        {
            _windowReadyEvent.WaitOne();
            int id = System.Threading.Interlocked.Increment(ref _id);
            _wnd.Invoke(new RegisterHotKeyDelegate(RegisterHotKeyInternal), _hwnd, id, (uint)modifiers, (uint)key);
            return id;
        }

        public static int RegisterHotKey(Hotkey hotkey)
        {
            return RegisterHotKey(hotkey.Keys, hotkey.Modifiers);
        }

        public static int RegisterHotKey(HotkeyCommand command, Hotkey hotkey)
        {
            var hotkeyId = RegisterHotKey(hotkey);
            var newHotkey = new RegisteredHotkey(hotkeyId, hotkey);
            if (RegisteredHotkeys.ContainsKey(command))
            {
                UnregisterHotKey(RegisteredHotkeys[command].Id);
                RegisteredHotkeys[command] = newHotkey;
            }
            else
            {
                RegisteredHotkeys.Add(command, newHotkey);
            }

            return hotkeyId;
        }

        public static void Initialize()
        {
            RegisterHotKey(HotkeyCommand.SelectScreenRegion1, new(Keys.D1, KeyModifiers.Control | KeyModifiers.Alt));
            RegisterHotKey(HotkeyCommand.CaptureScreenRegion1, new(Keys.D1, KeyModifiers.Alt));
            RegisterHotKey(HotkeyCommand.SelectScreenRegion2, new(Keys.D2, KeyModifiers.Control | KeyModifiers.Alt));
            RegisterHotKey(HotkeyCommand.CaptureScreenRegion2, new(Keys.D2, KeyModifiers.Alt));
            RegisterHotKey(HotkeyCommand.SelectScreenRegion3, new(Keys.D3, KeyModifiers.Control | KeyModifiers.Alt));
            RegisterHotKey(HotkeyCommand.CaptureScreenRegion3, new(Keys.D3, KeyModifiers.Alt));
            RegisterHotKey(HotkeyCommand.CaptureAllRegions, new(Keys.D0, KeyModifiers.Alt));
            HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
        }
        private static async void HotKeyManager_HotKeyPressed(object? sender, HotKeyEventArgs e)
        {
            Hotkey hotkeyPressed = new(Keys: e.Key, Modifiers: e.Modifiers);
            log.Debug($"Keys Pressed: {hotkeyPressed.Modifiers}+{hotkeyPressed.Keys}");
            var selectedCommand = RegisteredHotkeys.FirstOrDefault((hotkey) => hotkey.Value.Hotkey == hotkeyPressed).Key;
            log.Info($"Handling Command: {selectedCommand}");

            switch (selectedCommand)
            {
                case HotkeyCommand.None:
                    break;
                case HotkeyCommand.SelectScreenRegion1:
                    log.Info("Selecting screen region 1...");
                    Server.Instance.screen.SelectScreenRegion(0);
                    break;
                case HotkeyCommand.SelectScreenRegion2:
                    log.Info("Selecting screen region 2...");
                    Server.Instance.screen.SelectScreenRegion(1);
                    break;
                case HotkeyCommand.SelectScreenRegion3:
                    log.Info("Selecting screen region 3...");
                    Server.Instance.screen.SelectScreenRegion(2);
                    break;
                case HotkeyCommand.CaptureScreenRegion1:
                    await ReadScreenRegion(0);
                    break;
                case HotkeyCommand.CaptureScreenRegion2:
                    await ReadScreenRegion(1);
                    break;
                case HotkeyCommand.CaptureScreenRegion3:
                    await ReadScreenRegion(2);
                    break;
                case HotkeyCommand.CaptureAllRegions:
                    break;
            }
        }

        private async static Task ReadScreenRegion(int regionIndex)
        {
            if (Server.Instance.screen.SelectedRegionArea(regionIndex) > 9)
            {
                log.Info($"Reading screen region {regionIndex}...");
                var bmp = Server.Instance.screen.GetScreenRegion(regionIndex);
                await Server.Instance.Narrator.ReadImage(bmp);
            }
            else
            {
                log.Error($"Warning: Screen Area {regionIndex + 1} too small to take a picture. Please select one first.");
            }

        }

        public static void UnregisterHotKey(int id)
        {
            _wnd.Invoke(new UnRegisterHotKeyDelegate(UnRegisterHotKeyInternal), _hwnd, id);
        }

        delegate void RegisterHotKeyDelegate(IntPtr hwnd, int id, uint modifiers, uint key);
        delegate void UnRegisterHotKeyDelegate(IntPtr hwnd, int id);

        private static void RegisterHotKeyInternal(IntPtr hwnd, int id, uint modifiers, uint key)
        {
            RegisterHotKey(hwnd, id, modifiers, key);
        }

        private static void UnRegisterHotKeyInternal(IntPtr hwnd, int id)
        {
            UnregisterHotKey(_hwnd, id);
        }

        private static void OnHotKeyPressed(HotKeyEventArgs e)
        {
            if (HotKeyManager.HotKeyPressed != null)
            {
                HotKeyManager.HotKeyPressed(null, e);
            }
        }

        private static volatile MessageWindow _wnd;
        private static volatile IntPtr _hwnd;
        private static ManualResetEvent _windowReadyEvent = new ManualResetEvent(false);
        static HotKeyManager()
        {
            Thread messageLoop = new Thread(delegate ()
            {
                Application.Run(new MessageWindow());
            });
            messageLoop.Name = "MessageLoopThread";
            messageLoop.IsBackground = true;
            messageLoop.Start();
        }

        private class MessageWindow : Form
        {
            public MessageWindow()
            {
                _wnd = this;
                _hwnd = this.Handle;
                _windowReadyEvent.Set();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    HotKeyEventArgs e = new HotKeyEventArgs(m.LParam);
                    HotKeyManager.OnHotKeyPressed(e);
                }

                base.WndProc(ref m);
            }

            protected override void SetVisibleCore(bool value)
            {
                // Ensure the window never becomes visible
                base.SetVisibleCore(false);
            }

            private const int WM_HOTKEY = 0x312;
        }

        [DllImport("user32", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private static int _id = 0;
    }


    public class HotKeyEventArgs : EventArgs
    {
        public readonly Keys Key;
        public readonly KeyModifiers Modifiers;

        public HotKeyEventArgs(Keys key, KeyModifiers modifiers)
        {
            this.Key = key;
            this.Modifiers = modifiers;
        }

        public HotKeyEventArgs(IntPtr hotKeyParam)
        {
            uint param = (uint)hotKeyParam.ToInt64();
            Key = (Keys)((param & 0xffff0000) >> 16);
            Modifiers = (KeyModifiers)(param & 0x0000ffff);
        }
    }

    [Flags]
    public enum KeyModifiers
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
        NoRepeat = 0x4000
    }

    public enum HotkeyCommand
    {
        None = 0,
        SelectScreenRegion1,
        SelectScreenRegion2,
        SelectScreenRegion3,
        CaptureScreenRegion1,
        CaptureScreenRegion2,
        CaptureScreenRegion3,
        CaptureAllRegions
    }

    public record Hotkey(Keys Keys, KeyModifiers Modifiers);
    public record RegisteredHotkey(int Id, Hotkey Hotkey);
}
