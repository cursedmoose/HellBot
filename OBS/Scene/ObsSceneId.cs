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
        public const string MainScene = "Main Scene";
        public const string Characters = "Characters";

        public static ObsSceneId LastImage = new ObsSceneId(MainScene, 18);
        public static ObsSceneId Sheogorath = new ObsSceneId(Characters, 1);
        public static ObsSceneId DagothUr = new ObsSceneId(Characters, 2);
    } 
}
