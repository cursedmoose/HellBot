using System.Runtime.CompilerServices;

namespace TwitchBot.Assistant
{
    public static class PromptExtensions
    {
        private static readonly Random random = new();
        public static string Random(this List<string> prompts)
        {
            var randomPrompt = random.Next(prompts.Count);
            return prompts[randomPrompt];
        }
    }

    internal class Prompts
    {
        internal static List<string> Personas = new()
        {
            "{0}",
            "ecstatic {0}",
            "angry {0}",
            "bored {0}",
            "mischevious {0}",
            "sad {0}",
            "enthusiastic {0}",
            "playful {0}",
            "irratabile {0}",
            "melancholy {0}",
            "content {0}",
            "abrasive {0}",
            "confrontational {0}",
            "rude {0}",
            "abrasive confrontational and rude {0}",
            "twitch streamer {0}",
            "stupid {0}",
            "foolish {0}",
            "embarassing {0}",
            "fancy {0}",
            "bold {0}",
            "brave {0}",
            "aloof {0}",
            "nonsensical {0}",
            "bashful {0}",
            "flirty {0}"
        };

        internal static List<string> Reactions = new()
        {
            "pretend you know what's happening in this image. what will happen next?",
            "narrate what's happening like a nature documentary",
            "react to this image like a twitch streamer",
            "ask me what I'm going to do next",
            "tell me what to do next",
            "tell me what to do next in this video game. limit 10 words",
            "pretend I'm making the biggest mistake you've ever seen",
            "pretend I'm doing the coolest thing you've ever seen",
            "pretend I'm doing the dumbest thing you've ever seen in a video game"
        };
    }
}
