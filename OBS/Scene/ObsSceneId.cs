using TwitchBot.Config;
using TwitchBot.ElevenLabs;

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

        public static ObsSceneId Ads = new ObsSceneId(MainScene, 10);
        public static ObsSceneId LastImage = new ObsSceneId(MainScene, 18);

        public static ObsSceneId Sheogorath = new ObsSceneId(Characters, 1);
        public static ObsSceneId DagothUr = new ObsSceneId(Characters, 2);
        public static ObsSceneId AnnoyingFan = new ObsSceneId(Characters, 3);
        public static ObsSceneId Maiq = new ObsSceneId(Characters, 4);
        

        public static ObsSceneId getImageSource(string username)
        {
            switch (username.ToLower())
            {
                case TwitchConfig.Admins.Moose: return null;
                case TwitchConfig.Admins.Six: return DagothUr;
                case TwitchConfig.Admins.Sas: return Maiq;
                case TwitchConfig.Admins.Elise2: return AnnoyingFan;
            }

            return null;
        }
    }
}
