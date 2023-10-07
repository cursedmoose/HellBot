namespace TwitchBot.Discord
{
    internal class RichPresenceConstants
    {
        public static List<string> allowedFlavours = new()
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