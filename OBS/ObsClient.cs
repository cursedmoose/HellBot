using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types.Events;
using TwitchBot.OBS.Scene;
using TwitchBot.OBS.Websocket;

namespace TwitchBot.OBS
{
    public class ObsClient
    {
        private readonly HellbotObsWebsocket obs;
        private readonly static Logger log = new("OBS");
        public readonly bool Enabled = true;

        public ObsClient(bool enabled = true)
        {
            Enabled = enabled;

            obs = new HellbotObsWebsocket();

            obs.Connected += OnConnect;
            obs.Disconnected += OnDisconnect;
            obs.SceneItemEnableStateChanged += OnSceneChange;

            if (Enabled)
            {
                log.Info("Connecting to OBS...");
                try
                {
                    Connect();
                }
                catch (Exception ex)
                {
                    log.Error($"{ex}");
                }
            }
        }

        public void OnSceneChange(object? sender, SceneItemEnableStateChangedEventArgs e)
        {
            log.Info($"[Scene Changed] {e.SceneName}:{e.SceneItemId} = {e.SceneItemEnabled}");
        }

        public void Connect()
        {
            obs.ConnectAsync("ws://localhost:4455/", null);
        }

        public void Disconnect()
        {
            obs.Disconnect();
        }

        private void OnConnect(object? sender, EventArgs e)
        {
            if (!Enabled)
            {
                log.Info("Connected, but not enabled!?");
                return;
            }

            log.Info("Connected!");
        }

        private void OnDisconnect(object? sender, ObsDisconnectionInfo e)
        {
            log.Info($"Disconnected due to {e.DisconnectReason ?? "OBS is not running."}");
            Connect();
        }

        public void GetActiveSource()
        {
            PrintSceneList(ObsScenes.MainScene);
            PrintSceneList(ObsScenes.Characters);
            PrintSceneList(ObsScenes.Dice);
            PrintSceneList(ObsScenes.ScreenReader);
        }

        private void PrintSceneList(string scene)
        {
            var sceneItems = obs.GetSceneItemList(scene);
            foreach (var sceneItem in sceneItems)
            {
                log.Info($"{sceneItem.SourceName} : {sceneItem.SourceType} : {sceneItem.ItemId}");
            }
        }

        public void EnableScene(string sceneName, int itemId)
        {
            if (Enabled)
            {
                obs.SetSceneItemEnabled(sceneName, itemId, true);
            }
        }

        public void EnableScene(ObsSceneId scene)
        {
            EnableScene(scene.SceneName, scene.ItemId);
        }

        public void DisableScene(string sceneName, int itemId)
        {
            if (Enabled)
            {
                obs.SetSceneItemEnabled(sceneName, itemId, false);
            }
        }

        public void DisableScene(ObsSceneId scene)
        {
            DisableScene(scene.SceneName, scene.ItemId);
        }

        public string TakeSceneshot(string scene = ObsScenes.ScreenReader)
        {
            var filePath = "images/screenshots/screenreader.png";
            var path = Path.GetFullPath(filePath);
            obs.SaveSourceScreenshot(scene, "png", path);
            return filePath;
        }

    }
}
