using TwitchBot.Config;
using TwitchBot.Twitch;

namespace TwitchBot.ElevenLabs
{
    public record VoiceProfile(
        Voice Voice,
        float Stability,
        float Similarity,
        float Style = 0.66f
    )
    {
        //public VoiceProfile(Voice voice, float Stability, float Similarity, float Style = 0.66f)
        //    : this(voice.VoiceName, voice.VoiceId, Stability, Similarity, Style) { }
        public VoiceProfile Copy(float stability = -1f, float similarity = -1f, float style = -1f)
        {
            return new VoiceProfile(
                Voice: this.Voice, 
                // VoiceId: this.VoiceId,
                Stability: stability == -1f ? this.Stability : stability,
                Similarity: similarity == -1f ? this.Similarity : similarity,
                Style: style == -1f ? this.Style : style
            );
        }
    };

    public record struct Voice(string VoiceName, string VoiceId);

    public static class Voices
    {

        public static readonly Voice DagothUr = new(VoiceName: "Dagoth Ur", VoiceId: "d794HnJ8phTsWAjvnNHV");
        public static readonly Voice Maiq = new(VoiceName: "Maiq", VoiceId: "iC43838Gdr4CVJyB8Yw8");
        public static readonly Voice AnnoyingFan = new(VoiceName: "Annoying Fan", VoiceId: "ln6Vi3eg6PVlCOjhTq4j");
        public static readonly Voice Alduin = new(VoiceName: "Alduin", VoiceId: "raX9mfEZSZhxEnao1ANr");
        public static readonly Voice Sheogorath = new(VoiceName: "Sheogorath", VoiceId: "368JJn8VjXziM2teoiYc");
        public static readonly Voice Azura = new(VoiceName: "Azura", VoiceId: "qDSu8P0i7CeAaiHZmDuE");
        public static readonly Voice EsoProphet = new(VoiceName: "Prophet", VoiceId: "jePAZffIDg7pne1XGqjK");
        public static readonly Voice DrunkMale = new(VoiceName: "Drunk (M)", VoiceId: "vYoMFSJ7aibDefdtHAYC");
        public static readonly Voice TaraStrong = new(VoiceName: "Tara", VoiceId: "HLhL4bqJHbKk7F6PjcQN");
        public static readonly Voice Moira = new(VoiceName: "Moira", VoiceId: "Ute34dcnpIGwB8djz54Z");
        public static readonly Voice MasculineRumor = new(VoiceName: "Rumor (M)", VoiceId: "Fd3Nv8dOlr8Sd2GrltVV");
        public static readonly Voice FeminineRumor = new(VoiceName: "Rumor (F)", VoiceId: "wkMAp1DzoqsRcv2sVcBs");
        public static readonly Voice Herzog = new(VoiceName: "Werner", VoiceId: "mkB4DV5jXs2291mKTWgO");
    }

    public static class VoiceProfiles
    {
        const string ConfigFile = "voice";

        public static readonly VoiceProfile DagothUr = new(Voices.DagothUr, 0.33f, 0.66f);
        public static readonly VoiceProfile Maiq = new(Voices.Maiq, 0.25f, 0.25f);
        public static readonly VoiceProfile AnnoyingFan = new(Voices.AnnoyingFan, 0.1f, 0.25f);
        public static readonly VoiceProfile Alduin = new(Voices.Alduin, 0.6f, 0.75f);
        public static readonly VoiceProfile Sheogorath = new(Voices.Sheogorath, 0.12f, 0.8f);
        public static readonly VoiceProfile Azura = new(Voices.Azura, 0.25f, 0.75f);
        public static readonly VoiceProfile EsoProphet = new(Voices.EsoProphet, 0.5f, 0.8f);
        public static readonly VoiceProfile DrunkMale = new(Voices.DrunkMale, 0.2f, 0.66f);
        public static readonly VoiceProfile TaraStrong = new(Voices.TaraStrong, 0.2f, 0.8f);
        public static readonly VoiceProfile Moira = new(Voices.Moira, 0.2f, 0.8f);
        public static readonly VoiceProfile Werner = new(Voices.Herzog, 0.3f, 0.75f, 0.7f);

        public static readonly VoiceProfile MasculineRumor = new(Voices.MasculineRumor, 0.9f, 0.9f);
        public static readonly VoiceProfile FeminineRumor = new(Voices.FeminineRumor, 0.9f, 0.9f);

        public static readonly List<VoiceProfile> rumorMongers = new() { MasculineRumor, FeminineRumor };
        public static Dictionary<string, VoiceProfile> Profiles = new();

        public static void CreateProfiles()
        {
            CreateVoiceProfileConfig(TwitchConfig.Admins.Six, DagothUr);
            CreateVoiceProfileConfig(TwitchConfig.Admins.Sas, Maiq);
            CreateVoiceProfileConfig(TwitchConfig.Admins.Elise1, AnnoyingFan);
            CreateVoiceProfileConfig(TwitchConfig.Admins.Elise2, Azura);
            CreateVoiceProfileConfig(TwitchConfig.Admins.Dlique, Moira);
        }

        public static void LoadProfiles()
        {
            foreach (string admin in Permissions.Admin)
            {
                var profile = LoadVoiceProfileFromConfig(admin, DrunkMale);
                Console.WriteLine($"[VoiceProfiles] Loaded voice {profile.Voice.VoiceName} for {admin}");
            }
        }

        public static VoiceProfile? GetVoiceProfile(string username)
        {
            if (Profiles.ContainsKey(username)) {
                return Profiles[username]; 
            }
            else
            {
                return null;
            }
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

        public static VoiceProfile CreateVoiceProfileConfig(string username, VoiceProfile profile)
        {
            var agent = new FileGenerator.FileGenerator.UserAgent(username);
            Server.Instance.file.CreateAgentConfig(agent, ConfigFile, profile);
            Profiles[username] = profile;
            return profile;
        }

        public static VoiceProfile LoadVoiceProfileFromConfig(string username, VoiceProfile orDefaultTo)
        {
            var agent = new FileGenerator.FileGenerator.UserAgent(username);
            VoiceProfile? profile = Server.Instance.file.LoadAgentConfig<VoiceProfile>(agent, ConfigFile);
            if (profile != null)
            {
                Profiles[username] = profile;
                return profile;
            }
            else
            {
                Profiles[username] = orDefaultTo;
                return orDefaultTo;
            }
        }
    }
}
