using TwitchBot.Config;

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
        public const string Dice = "Dice";
        public const string ScreenReader = "ScreenReader";

        public static readonly ObsSceneId Ads = new(MainScene, 10);
        public static readonly ObsSceneId LastImage = new(MainScene, 18);

        public static readonly ObsSceneId Sheogorath = new(Characters, 1);
        public static readonly ObsSceneId DagothUr = new(Characters, 2);
        public static readonly ObsSceneId AnnoyingFan = new(Characters, 3);
        public static readonly ObsSceneId Maiq = new(Characters, 4);
        public static readonly ObsSceneId Werner = new(Characters, 6);

        public static readonly ObsSceneId DiceMain = new(MainScene, 23);
        public static readonly ObsSceneId DiceImage = new(Dice, 2);
        public static readonly ObsSceneId[] AllDice = {
            new(Dice, 5),
            new(Dice, 8),
            new(Dice, 9),
            new(Dice, 10),
            new(Dice, 11),
            new(Dice, 12),
            new(Dice, 13),
            new(Dice, 14),
            new(Dice, 15),
            new(Dice, 16),
            new(Dice, 17),
            new(Dice, 18),
            new(Dice, 19),
            new(Dice, 20),
            new(Dice, 21),
            new(Dice, 22),
            new(Dice, 23),
            new(Dice, 24),
            new(Dice, 25),
            new(Dice, 26)
        };

        public static readonly ObsSceneId Dice_01 = new(Dice, 5);
        public static readonly ObsSceneId Dice_02 = new(Dice, 8);
        public static readonly ObsSceneId Dice_03 = new(Dice, 9);
        public static readonly ObsSceneId Dice_04 = new(Dice, 10);
        public static readonly ObsSceneId Dice_05 = new(Dice, 11);
        public static readonly ObsSceneId Dice_06 = new(Dice, 12);
        public static readonly ObsSceneId Dice_07 = new(Dice, 13);
        public static readonly ObsSceneId Dice_08 = new(Dice, 14);
        public static readonly ObsSceneId Dice_09 = new(Dice, 15);
        public static readonly ObsSceneId Dice_10 = new(Dice, 16);
        public static readonly ObsSceneId Dice_11 = new(Dice, 17);
        public static readonly ObsSceneId Dice_12 = new(Dice, 18);
        public static readonly ObsSceneId Dice_13 = new(Dice, 19);
        public static readonly ObsSceneId Dice_14 = new(Dice, 20);
        public static readonly ObsSceneId Dice_15 = new(Dice, 21);
        public static readonly ObsSceneId Dice_16 = new(Dice, 22);
        public static readonly ObsSceneId Dice_17 = new(Dice, 23);
        public static readonly ObsSceneId Dice_18 = new(Dice, 24);
        public static readonly ObsSceneId Dice_19 = new(Dice, 25);
        public static readonly ObsSceneId Dice_20 = new(Dice, 26);
        // public static readonly ObsSceneId[] AllDice = { Dice_01, Dice_02 };

        public static readonly ObsSceneId ScreenReaderRegion = new(ScreenReader, 6);

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
