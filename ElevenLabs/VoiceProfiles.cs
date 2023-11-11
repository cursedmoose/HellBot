using TwitchBot.Config;

namespace TwitchBot.ElevenLabs
{
    public record VoiceProfile(
        string VoiceId,
        float Stability,
        float Similarity,
        float Style = 0.66f
        );

    public static class VoiceProfiles
    {
        public static readonly VoiceProfile DagothUr = new("d794HnJ8phTsWAjvnNHV", 0.33f, 0.66f);
        public static readonly VoiceProfile Maiq = new("iC43838Gdr4CVJyB8Yw8", 0.25f, 0.25f);
        public static readonly VoiceProfile AnnoyingFan = new("ln6Vi3eg6PVlCOjhTq4j", 0.1f, 0.25f);
        public static readonly VoiceProfile Alduin = new("raX9mfEZSZhxEnao1ANr", 0.6f, 0.75f);
        public static readonly VoiceProfile Sheogorath = new("368JJn8VjXziM2teoiYc", 0.12f, 0.8f);
        public static readonly VoiceProfile Azura = new("qDSu8P0i7CeAaiHZmDuE", 0.25f, 0.75f);
        public static readonly VoiceProfile EsoProphet = new("jePAZffIDg7pne1XGqjK", 0.5f, 0.8f);
        public static readonly VoiceProfile DrunkMale = new("vYoMFSJ7aibDefdtHAYC", 0.2f, 0.66f);
        public static readonly VoiceProfile TaraStrong = new("HLhL4bqJHbKk7F6PjcQN", 0.2f, 0.8f);
        public static readonly VoiceProfile Moira = new("Ute34dcnpIGwB8djz54Z", 0.2f, 0.8f);

        public static readonly VoiceProfile MasculineRumor = new("Fd3Nv8dOlr8Sd2GrltVV", 0.9f, 0.9f);
        public static readonly VoiceProfile FeminineRumor = new("wkMAp1DzoqsRcv2sVcBs", 0.9f, 0.9f);

        public static readonly List<VoiceProfile> rumorMongers = new() { MasculineRumor, FeminineRumor };

        public static VoiceProfile? GetVoiceProfile(string username)
        {
            return username.ToLower() switch
            {
                TwitchConfig.Admins.Moose => null,
                TwitchConfig.Admins.Six => DagothUr,
                TwitchConfig.Admins.Sas => Maiq,
                TwitchConfig.Admins.Elise1 => Azura,
                TwitchConfig.Admins.Elise2 => AnnoyingFan,
                TwitchConfig.Admins.Dlique => Moira,
                _ => null,
            };
        }

        public static List<VoiceProfile> GetVoices(string username)
        {
            return username.ToLower() switch
            {
                TwitchConfig.Admins.Moose => new List<VoiceProfile>(),
                TwitchConfig.Admins.Six => new List<VoiceProfile> { DagothUr },
                TwitchConfig.Admins.Sas => new List<VoiceProfile> { Maiq },
                TwitchConfig.Admins.Elise1 or TwitchConfig.Admins.Elise2 => new List<VoiceProfile> { Azura, AnnoyingFan },
                TwitchConfig.Admins.Dlique => new List<VoiceProfile> { Alduin, EsoProphet },
                _ => new List<VoiceProfile>(),
            };
        }

        public static VoiceProfile GetRumorVoiceProfile()
        {
            var randomSelection = new Random().Next(rumorMongers.Count);
            return rumorMongers[randomSelection];
        }

        public static int GetRumorVoiceSelection()
        {
            return new Random().Next(rumorMongers.Count);
        }
    }
}
