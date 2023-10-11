using NAudio.MediaFoundation;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using TwitchBot.OBS.Scene;

namespace TwitchBot.OBS
{
    public class ObsClient
    {
        private OBSWebsocket obs;
        private static Logger log = new Logger("OBS");
        private bool Enabled = true;

        public ObsClient(bool enabled = true)
        {
            Enabled = enabled;

            obs = new OBSWebsocket();
            obs.Connected += onConnect;
            obs.Disconnected += onDisconnect;

            if (Enabled)
            {
                obs.ConnectAsync("ws://localhost:4455/", "");
            }
        }

        public void Disconnect()
        {
            obs.Disconnect();
        }

        private void onConnect(object? sender, EventArgs e)
        {
            if (!Enabled)
            {
                log.info("Connected, but not enabled!?");
                return;
            }

            log.info("Connected!");
        }

        private void onDisconnect(object? sender, ObsDisconnectionInfo e)
        {
            log.info($"Disconnected due to {e.DisconnectReason}");
        }

        public void GetActiveSource()
        {
            var sceneItems = obs.GetSceneItemList("Main Scene");
            foreach (var sceneItem in sceneItems)
            {
                log.info($"{sceneItem.SourceName} : {sceneItem.SourceType} : {sceneItem.ItemId}");
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

    }
}
