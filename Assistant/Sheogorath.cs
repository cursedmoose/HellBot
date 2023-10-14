using System.Globalization;
using System.Text;
using TwitchBot.Assistant.AI;
using TwitchBot.Assistant.Polls;
using TwitchBot.ElevenLabs;
using TwitchBot.OBS.Scene;
using static TwitchBot.ChatGpt.ChatGpt;

namespace TwitchBot.Assistant
{
    internal class Sheogorath : Assistant
    {
        public Sheogorath() : base(
                name: "Sheogorath",
                voice: VoiceProfiles.Sheogorath,
                sceneId: ObsScenes.Sheogorath
            )
        {

        }

        readonly List<Actions> AI_CAPABILITIES = new()
            {
                Actions.Ban,
                Actions.Chat,
                // Actions.ChangeTitle,
                Actions.RunPoll,
                Actions.CreateReward,
                Actions.PaintPicture
            };

        private static DateTime LastPollTime = DateTime.MinValue;
        private static readonly Dictionary<string, string> rewardsCreated = new();

        public override string GetSystemPersona()
        {
            var choice = new Random().Next(0, PersonaPrompts.All.Count);
            return string.Format(PersonaPrompts.All[choice], Name);
        }

        public override async void CleanUp()
        {
            await DeleteAllRewards();
        }
        protected override async Task AI()
        {
            var time = DateTime.Now;

            var mischief = new Random().Next(3) == 0;
            if (mischief)
            {
                var actionToTake = AI_CAPABILITIES[new Random().Next(AI_CAPABILITIES.Count)];
                log.Info($"Mischief!: {actionToTake}");
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
                            log.Info("Poll is on cooldown!");
                        }
                        break;
                    case Actions.CreateReward:
                        if (rewardsCreated.Count <= 0)
                        {
                            await CreateReward();
                            log.Info($"{rewardsCreated.Count} rewards available!");
                        }
                        else
                        {
                            if (new Random().Next(5) == 0)
                            {
                                await CreateReward();
                                log.Info($"{rewardsCreated.Count} rewards available!");
                            }
                            else
                            {
                                await DeleteReward();
                                log.Info($"{rewardsCreated.Count} rewards available!");
                            }
                        }
                        break;
                    case Actions.PaintPicture:
                        await PaintPicture();
                        break;
                }
            }
            else
            {
                await Chatter();
            }

            await Task.Delay(300 * 1_000);
            return;
        }

        public override async Task<bool> CreatePoll()
        {
            PlayTts("How about a poll?");
            string response = await Server.Instance.chatgpt.GetResponseText(
                persona: Persona,
                chatPrompt: Poll.PollPrompt
            );
            log.Info(response);

            Poll poll = PollParser.parsePoll(response);
            return await Server.Instance.twitch.CreatePoll(
                title: poll.Title,
                choices: poll.Choices
            );
        }

        public override async Task<bool> AnnouncePoll(string title, List<string> options)
        {
            StringBuilder pollMessageBuilder = new();
            pollMessageBuilder.Append($"Title:{title}\r\n");
            for (int x = 0; x < options.Count; x++)
            {
                pollMessageBuilder.Append($"Option {x+1}: {options[x]}\r\n");
            }

            string pollMessage = pollMessageBuilder.ToString();

            var messages = ConvertToMessages(new List<string>() {
                    pollMessage,
                    Poll.PollAnnounce
            });
            var response = await Server.Instance.chatgpt.GetResponseText(Persona, messages);
            log.Info(response);
            PlayTts(response);

            return true;
        }

        public override async Task<bool> ConcludePoll(string title, string winner)
        {
            PlayTts("The results are in...");
            var prompt = String.Format(Poll.PollEndPrompt, title, winner);
            return await Server.Instance.chatgpt.GetResponse(Persona, prompt);
        }


        public override async void WelcomeBack(string gameTitle)
        {
            log.Info($"Oh, welcome back to {gameTitle}...");
            var welcomeBack = $"welcome me back to {gameTitle}";
            await Server.Instance.chatgpt.GetResponse(Persona, welcomeBack);
        }

        public override async void WelcomeFollower(string username)
        {
            await Server.Instance.chatgpt.GetResponse(Persona, $"welcome new follower \"{username}\"");
        }

        public override async void WelcomeSubscriber(string username, int length)
        {
            await Server.Instance.chatgpt.GetResponse(Persona, $"thank \"{username}\" for subscribing for {length} months");
        }

        public override async Task<bool> ChannelRewardClaimed(string byUsername, string rewardTitle, int cost)
        {
            var prompt = $"react to \"{byUsername}\" redeeming channel reward \"{rewardTitle}\"";
            return await Server.Instance.chatgpt.GetResponse(Persona, prompt);
        }

        public override async Task<bool> ChangeTitle()
        {
            //PlayTts("How about a new stream title? Maybe...");
            var currentGame = await Server.Instance.twitch.GetCurrentGame();
            var prompt = $"new title for my \"{currentGame}\" stream. limit 5 words.";
            var newTitle = await Server.Instance.chatgpt.GetResponseText(Persona, prompt);
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
                    await Server.Instance.chatgpt.GetResponse(Persona, prompt);

                    banned = true;
                }
            } while (!banned);

            return;
            // allChatters.Where(chatter => chatter.UserName != "CursedMoose" && chatter.UserName != "Nightbot");
        }

        public async Task<string> CreateReward()
        {
            var rewardTitle = await Server.Instance.chatgpt.GetResponseText(Persona, "create a new point reward. limit 5 words");
            var rewardCost = new Random().Next(500, 10_000);
            var advertisementPrompt = $"advertise new reward \"{rewardTitle}\" for {rewardCost} points.";
            var newReward = await Server.Instance.twitch.CreateCustomReward(rewardTitle, rewardCost);
            await Server.Instance.chatgpt.GetResponse(Persona, advertisementPrompt);
            rewardsCreated.Add(rewardTitle, newReward);

            log.Info($"{rewardsCreated.Count} rewards available!");

            return newReward;
        }

        public async Task<string> DeleteReward()
        {
            if (rewardsCreated.Count > 0)
            {
                var rewardToDelete = rewardsCreated.ElementAt(new Random().Next(rewardsCreated.Count));
                var revocationPrompt = $"announce the reward \"{rewardToDelete.Key}\" is being discontinued";
                await Server.Instance.chatgpt.GetResponse(Persona, revocationPrompt);
                var deleted = await Server.Instance.twitch.DeleteCustomReward(rewardToDelete.Value);

                log.Info($"{rewardsCreated.Count} rewards available!");
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

        public async Task PaintPicture()
        {
            //Server.Instance.elevenlabs.playTts("Let's paint a picture!", Voice);
            PlayTts("Let's paint a picture!");

            var getPrompt = "make an image prompt. limit 5 words";
            var imagePrompt = await Server.Instance.chatgpt.GetResponseText(Persona, getPrompt);
            var image = await Server.Instance.chatgpt.GetImage(imagePrompt);

            ObsScenes.LastImage.Enable();
            var announcePrompt = $"announce your new painting \"{imagePrompt}\"";
            var announcement = await Server.Instance.chatgpt.GetResponse(Persona, announcePrompt);
            // var shortUrl = await Server.Instance.shortenUrl(image);
            if (announcement)
            {
                Server.Instance.twitch.Respond($"\"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(imagePrompt)}\": {image}");
            }
            ObsScenes.LastImage.Disable();

        }

        public override async Task<bool> RunAd(int adSeconds = 5)
        {
            var run = "apologize that it is time for an ad";
            await Server.Instance.chatgpt.GetResponse(Persona, run);
            ObsScenes.Ads.Enable();
            await Server.Instance.twitch.RunAd(adSeconds);
            await Task.Delay((adSeconds + 10) * 1000);
            ObsScenes.Ads.Disable();
            var end = "rejoice that the ad is over";
            await Server.Instance.chatgpt.GetResponse(Persona, end);

            return true;
        }

    }
}
