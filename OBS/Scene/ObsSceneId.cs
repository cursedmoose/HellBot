namespace TwitchBot.OBS.Scene
{
    public record ObsSceneId(string SceneName, int ItemId)
    { 
        public void Enable()
        {
            Server.Instance.obs.EnableScene(this);
        }

        public void Disable()
        {
            Server.Instance.obs.DisableScene(this);
        }
    }
    public static class ObsScenes
    {
        const string MainScene = "Main Scene";

        public static ObsSceneId LastImage = new ObsSceneId(MainScene, 18);
        public static ObsSceneId Sheogorath = new ObsSceneId(MainScene, 15);
    } 
}
