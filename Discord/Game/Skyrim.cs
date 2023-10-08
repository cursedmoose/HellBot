using Discord;

namespace TwitchBot.Discord.Game
{
    internal class Skyrim
    {
        public SkyrimActivity parsePresence(RichGame game)
        {
            var flavorTokens = game.State.Split(",");
            if (flavorTokens.Length == 3)
            {
                return new SkyrimActivity(flavour: flavorTokens[0].Trim(), location: flavorTokens[1].Trim(), worldspace: flavorTokens[2].Trim());
            }
            else if (flavorTokens.Length == 2)
            {
                return new SkyrimActivity(flavour: flavorTokens[0].Trim(), location: flavorTokens[1].Trim());
            }
            else if (flavorTokens.Length == 1)
            {
                return new SkyrimActivity(flavour: flavorTokens[0].Trim(), "");
            }
            else
            {
                return new SkyrimActivity("", "");
            }
        }


        public static List<string> AllowedFlavours = new()
        {
            "Died",
            "Ragdolling",
        };

        public static List<string> Flavour = new()
        {
            "Bartering",
            "Reading",
            "Searching",
            "Using",
            "Giving gift",
            "Lockpicking",
            "Training",
            "Talking",
            "Riding",
            "Died",
            "Fighting",
            "Sneaking",
            "Flying",
            "Swimming",
        };

    }

    public record SkyrimActivity(
        string flavour,
        string location,
        string worldspace = ""
    );
}
