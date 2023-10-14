using TwitchBot.ElevenLabs;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class SetParameter : CommandHandler
    {
        private const string COMMAND = "!set";
        bool CommandHandler.canHandle(TwitchClient client, ChatMessage message)
        {
            return false;
        }

        void CommandHandler.handle(TwitchClient client, ChatMessage message)
        {
            var tokens = message.Message.Split(' ');
            if (tokens.Length <= 2 ) {
                Console.WriteLine($"Not enough params to handle Set command: {message.Message}");
                return;
            } 
            else
            {
                var subComand = tokens[1];
                var subCommandParams = tokens.ToList();
                subCommandParams.RemoveRange(0, 2);
                Console.WriteLine($"Setting variable {subComand} with {subCommandParams.Count} params.");

                if (subComand == "stability" && subCommandParams.Count >= 1)
                {
                    Console.WriteLine($"Setting variable {subComand} to {subCommandParams[0]}.");
                    var oldVoice = VoiceProfiles.GetVoiceProfile(message.Username);
                    if (oldVoice != null)
                    {
                        var newVoice = new VoiceProfile(oldVoice.VoiceId, int.Parse(subCommandParams[0]) / 100f, oldVoice.Similarity);
                    }
                }
            }
        }
    }
}
