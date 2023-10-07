using TwitchBot.Config;

namespace TwitchBot.ElevenLabs
{
    public record VoiceProfile(
        string voiceId,
        float stability,
        float similarity
        );

    public static class VoiceProfiles
    {
        public static VoiceProfile DagothUr = new("d794HnJ8phTsWAjvnNHV", 0.33f, 0.66f);
        public static VoiceProfile Maiq = new("iC43838Gdr4CVJyB8Yw8", 0.25f, 0.25f);
        public static VoiceProfile AnnoyingFan = new("ln6Vi3eg6PVlCOjhTq4j", 0.1f, 0.25f);
        public static VoiceProfile Alduin = new("raX9mfEZSZhxEnao1ANr", 0.6f, 0.75f);
        public static VoiceProfile Sheogorath = new("368JJn8VjXziM2teoiYc", 0.12f, 0.8f);
        public static VoiceProfile Azura = new("qDSu8P0i7CeAaiHZmDuE", 0.25f, 0.75f);
        public static VoiceProfile EsoProphet = new("jePAZffIDg7pne1XGqjK", 0.5f, 0.8f);
        public static VoiceProfile DrunkMale = new("vYoMFSJ7aibDefdtHAYC", 0.2f, 0.66f);

        public static VoiceProfile MasculineRumor = new("Fd3Nv8dOlr8Sd2GrltVV", 0.9f, 0.9f);
        public static VoiceProfile FeminineRumor = new("wkMAp1DzoqsRcv2sVcBs", 0.9f, 0.9f);

        public static List<VoiceProfile> rumorMongers = new List<VoiceProfile>() { MasculineRumor, FeminineRumor };

        public static VoiceProfile getVoiceProfile(string username)
        {
            switch (username.ToLower())
            {
                case TwitchConfig.Admins.Moose: return null;
                case TwitchConfig.Admins.Six: return DagothUr;
                case TwitchConfig.Admins.Sas: return Maiq;
                case TwitchConfig.Admins.Elise1: return Azura;
                case TwitchConfig.Admins.Elise2: return AnnoyingFan;
                case TwitchConfig.Admins.Dlique: return DrunkMale;
            }

            return null;
        }

        public static List<VoiceProfile> getVoices(string username)
        {
            switch (username.ToLower())
            {
                case TwitchConfig.Admins.Moose: return new List<VoiceProfile>();
                case TwitchConfig.Admins.Six: return new List<VoiceProfile> { DagothUr };
                case TwitchConfig.Admins.Sas: return new List<VoiceProfile> { Maiq };
                case TwitchConfig.Admins.Elise1:
                case TwitchConfig.Admins.Elise2: return new List<VoiceProfile> { Azura, AnnoyingFan };
                case TwitchConfig.Admins.Dlique: return new List<VoiceProfile> { Alduin, EsoProphet };
            }

            return new List<VoiceProfile>();
        }

        public static VoiceProfile getRumorVoiceProfile()
        {
            var randomSelection = new Random().Next(rumorMongers.Count);
            return rumorMongers[randomSelection];
        }

        public static int getRumorVoiceSelection()
        {
            return new Random().Next(rumorMongers.Count);
        }
    }
}
