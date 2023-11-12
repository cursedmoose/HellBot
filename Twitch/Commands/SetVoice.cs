using System.Runtime.CompilerServices;
using TwitchBot.ElevenLabs;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Interfaces;

namespace TwitchBot.Twitch.Commands
{
    internal class SetVoice : CommandHandler
    {
        public SetVoice() : base(command: "!voice", users: PermissionGroup.Admin) { }

        public override void Handle(TwitchIrcBot client, ChatMessage message)
        {
            var subCommand = StripCommandFromMessage(message);
            if (string.IsNullOrEmpty(subCommand)) 
            {
                SendVoiceSettings(client, message);
            }
            else if (subCommand.StartsWith("save", CompareBy))
            {
                HandleSaveVoice(message.Username);
                client.RespondTo(message, $"@{message.Username}: Voice saved.");
            }
            else if (subCommand.StartsWith("reset", CompareBy))
            {
                HandleResetVoice(message.Username);
                SendVoiceSettings(client, message);
            }
            else
            {
                try
                {
                    var voiceParams = subCommand.Split(" ");
                    int stability = int.Parse(voiceParams[0]);
                    int similarity = int.Parse(voiceParams[1]);
                    int style = int.Parse(voiceParams[2]);
                    if (IsValidNumber(stability) && IsValidNumber(similarity) && IsValidNumber(style))
                    {
                        HandleSetVoiceParams(message.Username, stability, similarity, style);
                        SendVoiceSettings(client, message);
                    }
                    else
                    {
                        throw new Exception("bad numbers");
                    }
                }
                catch (Exception ex)
                {
                    client.RespondTo(message, $"@{message.Username} this commands requires either \"save\", \"reset\", or 3 numbers from 0-100");
                }
            }      
        }

        private bool IsValidNumber(int number)
        {
            return number >= 0 && number <= 100;
        }

        private void HandleSaveVoice(string username)
        {
            var voiceProfile = VoiceProfiles.Profiles.GetValueOrDefault(username, VoiceProfiles.DrunkMale);
            VoiceProfiles.CreateVoiceProfileConfig(username, voiceProfile);
        }

        private void HandleResetVoice(string username)
        {
            var voiceProfile = VoiceProfiles.LoadVoiceProfileFromConfig(username, VoiceProfiles.DrunkMale);
            if (VoiceProfiles.Profiles.ContainsKey(username))
            {
                VoiceProfiles.Profiles[username] = voiceProfile;
            } 
            else
            {
                VoiceProfiles.Profiles.Add(username, voiceProfile);
            }
        }

        private void HandleSetVoiceParams(string username, int stability, int similarity, int style)
        {
            var realStability = stability / 100f;
            var realSimilarity = similarity / 100f;
            var realStyle = style / 100f;

            var oldVoiceProfile = VoiceProfiles.Profiles.GetValueOrDefault(username, VoiceProfiles.DrunkMale);
            var newVoiceProfile = oldVoiceProfile.Copy(realStability, realSimilarity, realStyle);
            if (VoiceProfiles.Profiles.ContainsKey(username))
            {
                VoiceProfiles.Profiles[username] = newVoiceProfile;
            }
            else
            {
                VoiceProfiles.Profiles.Add(username, newVoiceProfile);
            }
        }

        private void SendVoiceSettings(TwitchIrcBot client, ChatMessage message) 
        {
            try
            {
                var voiceProfile = VoiceProfiles.Profiles[message.Username];
                client.RespondTo(message, $"@{message.Username}'s Current Voice Settings: "
                    + $"Stability={(int)(voiceProfile.Stability * 100)} "
                    + $"Similarity={(int)(voiceProfile.Similarity * 100)} "
                    + $"Style={(int)(voiceProfile.Style * 100)} ");
            }
            catch (Exception ex)
            {
                client.RespondTo(message, $"@{message.Username} is missing a voice. Please yell at me to fix this.");
            }
        }
    }
}
