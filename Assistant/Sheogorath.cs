using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchBot.ElevenLabs;

namespace TwitchBot.Assistant
{
    internal class Sheogorath : Assistant
    {
        public Sheogorath() : base("Sheogorath", VoiceProfiles.Sheogorath)
        {

        }

        public override bool RunPoll(string pollContext)
        {
            throw new NotImplementedException();
        }

        public override void WelcomeBack(string gameTitle)
        {
            Log($"Oh, welcome back to {gameTitle}...");
            var welcomeBack = $"welcome me back to {gameTitle}. limit 25 words.";
            Server.Instance.chatgpt.getResponse("Shegorath", welcomeBack);
        }


    }
}
