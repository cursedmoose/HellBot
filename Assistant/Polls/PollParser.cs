﻿using System.Text.RegularExpressions;

namespace TwitchBot.Assistant.Polls
{
    public class Poll
    {
        public const string PollPrompt = "fill out the following poll\r\nTitle:\r\nOption 1:\r\nOption 2:\r\nOption 3:";
        public const string PollAnnounce = "announce the poll. limit 25 words";
        public const string PollEndPrompt = "the poll \"{0}\" ended. \"{1}\" was the winner";

        public string Title = "";
        public List<string> Choices = new();
    }
    internal class PollParser
    {
        public static Poll parsePoll(string rawPoll)
        {
            Poll poll = new();

            var pollTokens = rawPoll.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries);

            List<string> options = new();
            for (var x = 0; x < pollTokens.Length; x++)
            {
                if (pollTokens[x].Contains("options:", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (options.Count > 3) { continue; }
                if (pollTokens[x].Contains("title:", StringComparison.OrdinalIgnoreCase))
                {
                    poll.Title = pollTokens[x].Split(":")[1].Trim();
                }
                else
                {
                    if (x == 0)
                    {
                        poll.Title = pollTokens[x].Trim();
                    }
                    else
                    {
                        var optionTokens = pollTokens[x].Split(":");
                        if (optionTokens.Length > 1)
                        {
                            poll.Choices.Add(limitCharacters(optionTokens[1]));
                        }
                        else
                        {
                            poll.Choices.Add(limitCharacters(optionTokens[0]));
                        }
                    }
                }
            }


            return poll;
        }

        private static string limitCharacters(string original, int characterLimit = 25)
        {
            return Regex.Match(original, "^(.{0,25})(?: |$)").Value;
        }
    }
}
