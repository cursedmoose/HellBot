using System.Text;
using TwitchBot.Assistant.Polls;
using TwitchBot.ElevenLabs;
using static TwitchBot.ChatGpt.ChatGpt;

namespace TwitchBot.Assistant
{
    internal class Sheogorath : Assistant
    {
        public Sheogorath() : base("Sheogorath", VoiceProfiles.Sheogorath)
        {

        }

        public override string GetSystemPersona()
        {
            var choice = new Random().Next(0, Prompts.All.Count);
            return string.Format(Prompts.All[choice], Name);
        }

        public override async Task<bool> CreatePoll()
        {
            PlayTts("How about a poll?");
            string response = await Server.Instance.chatgpt.getResponseText(
                persona: Persona,
                chatPrompt: Poll.PollPrompt
            );
            Log(response);

            Poll poll = PollParser.parsePoll(response);
            return await Server.Instance.twitch.CreatePoll(
                title: poll.Title,
                choices: poll.Choices
            );
        }

        public override async Task<bool> AnnouncePoll(string title, List<string> options)
        {
            StringBuilder pollMessageBuilder = new StringBuilder();
            pollMessageBuilder.Append($"Title:{title}\r\n");
            for (int x = 0; x < options.Count; x++)
            {
                pollMessageBuilder.Append($"Option {x+1}: {options[x]}\r\n");
            }

            string pollMessage = pollMessageBuilder.ToString();

            var messages = convertToMessages(new List<string>() {
                    pollMessage,
                    Poll.PollAnnounce
            });
            var response = await Server.Instance.chatgpt.getResponseText(Persona, messages);
            Log(response);
            PlayTts(response);

            return true;
        }

        public override async Task<bool> ConcludePoll(string title, string winner)
        {
            PlayTts("The results are in...");
            var prompt = String.Format(Poll.PollEndPrompt, title, winner);
            return await Server.Instance.chatgpt.getResponse(Persona, prompt);
        }


        public override async void WelcomeBack(string gameTitle)
        {
            Log($"Oh, welcome back to {gameTitle}...");
            var welcomeBack = $"welcome me back to {gameTitle}";
            await Server.Instance.chatgpt.getResponse(Persona, welcomeBack);
        }

        public override async void WelcomeFollower(string username)
        {
            await Server.Instance.chatgpt.getResponse(Persona, $"welcome new follower \"{username}\"");
        }

        public override async void WelcomeSubscriber(string username, int length)
        {
            await Server.Instance.chatgpt.getResponse(Persona, $"thank \"{username}\" for subscribing for {length} months");
        }

        public override async Task<bool> ChannelRewardClaimed(string byUsername, string rewardTitle, int cost)
        {
            var prompt = $"react to \"{byUsername}\" redeeming channel reward \"{rewardTitle}\"";
            return await Server.Instance.chatgpt.getResponse(Persona, prompt);
        }

    }
}
