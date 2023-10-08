using System.Text;
using TwitchBot.Assistant.AI;
using TwitchBot.Assistant.Polls;
using TwitchBot.ElevenLabs;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using static TwitchBot.ChatGpt.ChatGpt;

namespace TwitchBot.Assistant
{
    internal class Sheogorath : Assistant
    {
        public Sheogorath() : base("Sheogorath", VoiceProfiles.Sheogorath)
        {

        }

        List<Actions> AI_CAPABILITIES = new()
            {
                Actions.Ban,
                Actions.Chat,
                Actions.ChangeTitle,
                Actions.RunPoll,
                Actions.CreateReward
            };

        private static DateTime LastPollTime = DateTime.MinValue;
        private static Dictionary<string, string> rewardsCreated = new();

        public override string GetSystemPersona()
        {
            var choice = new Random().Next(0, PersonaPrompts.All.Count);
            return string.Format(PersonaPrompts.All[choice], Name);
        }
        protected override async Task AI()
        {
            var time = DateTime.Now;

            var mischief = new Random().Next(1) == 0;
            if (mischief)
            {
                var actionToTake = AI_CAPABILITIES[new Random().Next(AI_CAPABILITIES.Count)];
                Log($"Mischief!: {actionToTake}");
                switch (actionToTake)
                {
                    case Actions.Ban:
                        await BanRandomUser();
                        break;
                    case Actions.Chat:
                        await Chatter();
                        break;
                    case Actions.ChangeTitle:
                        await ChangeTitle();
                        break;
                    case Actions.RunPoll:
                        if (LastPollTime.AddMinutes(5) < time)
                        {
                            LastPollTime = time;
                            await CreatePoll();
                        }
                        else
                        {
                            Log("Poll is on cooldown!");
                        }
                        break;
                    case Actions.CreateReward:
                        if (rewardsCreated.Count <= 0)
                        {
                            await CreateReward();
                        }
                        else
                        {
                            if (new Random().Next(3) == 0)
                            {
                                await CreateReward();
                            }
                            else
                            {
                                await DeleteReward();
                            }
                        }
                        break;
                }
            }
            else
            {
                await Chatter();
            }

            // Log($"Hello at {time}");
            // await ChangeTitle();
            // await BanRandomUser();
            await Task.Delay(150 * 1_000);
            return;
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

        public override async Task<bool> ChangeTitle()
        {
            //PlayTts("How about a new stream title? Maybe...");
            var currentGame = await Server.Instance.twitch.GetCurrentGame();
            var prompt = $"new title for my \"{currentGame}\" stream. limit 5 words.";
            var newTitle = await Server.Instance.chatgpt.getResponseText(Persona, prompt);
            PlayTts($"How about a new stream title? Maybe... {newTitle}!");
            Server.Instance.twitch.ChangeTitle(newTitle);

            return true;
        }

        private async Task BanRandomUser()
        {
            var random = new Random();
            var allChatters = await Server.Instance.twitch.GetChatters();
            var banned = false;

            do
            {
                var userToBan = allChatters[random.Next(allChatters.Count)];
                if (userToBan.UserName != "CursedMoose" && userToBan.UserName != "Nightbot")
                {
                    var prompt = $"pretend you are banning user \"{userToBan.UserName}\"";
                    await Server.Instance.chatgpt.getResponse(Persona, prompt);

                    banned = true;
                }
            } while (!banned);

            return;
            // allChatters.Where(chatter => chatter.UserName != "CursedMoose" && chatter.UserName != "Nightbot");
        }

        public async Task<string> CreateReward()
        {
            var rewardTitle = await Server.Instance.chatgpt.getResponseText(Persona, "create a new point reward. limit 5 words");
            var rewardCost = new Random().Next(10_000);
            var advertisementPrompt = $"advertise new reward \"{rewardTitle}\" for {rewardCost} points.";
            var newReward = await Server.Instance.twitch.CreateCustomReward(rewardTitle, rewardCost);
            await Server.Instance.chatgpt.getResponse(Persona, advertisementPrompt);
            rewardsCreated.Add(rewardTitle, newReward);

            return newReward;
        }

        public async Task<string> DeleteReward()
        {
            if (rewardsCreated.Count > 0)
            {
                var rewardToDelete = rewardsCreated.ElementAt(new Random().Next(rewardsCreated.Count));
                var revocationPrompt = $"announce the reward \"{rewardToDelete.Key}\" is being discontinued";
                await Server.Instance.chatgpt.getResponse(Persona, revocationPrompt);
                var deleted = await Server.Instance.twitch.DeleteCustomReward(rewardToDelete.Value);
                if (deleted)
                {
                    rewardsCreated.Remove(rewardToDelete.Key);
                    return rewardToDelete.Value;
                }
            }

            return "";
        }

        private async Task<bool> DeleteAllRewards()
        {
            if (rewardsCreated.Count > 0)
            {
                foreach (var reward in rewardsCreated)
                {
                    var deleted = await Server.Instance.twitch.DeleteCustomReward(reward.Value);
                    if (deleted)
                    {
                        rewardsCreated.Remove(reward.Key);
                    }
                }
            }

            return true;
        }

    }
}
