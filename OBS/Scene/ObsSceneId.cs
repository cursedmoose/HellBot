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

        public static readonly ObsSceneId Ads = new(MainScene, 10);
        public static readonly ObsSceneId LastImage = new(MainScene, 18);

        public static readonly ObsSceneId Sheogorath = new(Characters, 1);
        public static readonly ObsSceneId DagothUr = new(Characters, 2);
        public static readonly ObsSceneId AnnoyingFan = new(Characters, 3);
        public static readonly ObsSceneId Maiq = new(Characters, 4);
        

        public static ObsSceneId? GetImageSource(string username)
        {
            return username.ToLower() switch
            {
                TwitchConfig.Admins.Moose => null,
                TwitchConfig.Admins.Six => DagothUr,
                TwitchConfig.Admins.Sas => Maiq,
                TwitchConfig.Admins.Elise2 => AnnoyingFan,
                _ => null
            };
        }
    }
}
