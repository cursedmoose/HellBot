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
            AI_Actions.Add(Actions.Ban, Chatter);
            AI_Actions.Add(Actions.Chat, Chatter);
            AI_Actions.Add(Actions.ChangeTitle, Chatter);
            AI_Actions.Add(Actions.RunPoll, Chatter);
            AI_Actions.Add(Actions.CreateReward, Chatter);
            AI_Actions.Add(Actions.PaintPicture, Chatter);
            AI_Actions.Add(Actions.ReactToScreen, Chatter);
            AI_Actions.Add(Actions.RequestNarration, () => { return ReactToCurrentScreen(); } );
        }

        readonly List<Actions> AI_CAPABILITIES = new()
            {
                // Actions.Ban,
                // Actions.Chat,
                // Actions.ChangeTitle,
                Actions.RunPoll,
                Actions.CreateReward,
                Actions.PaintPicture,
                Actions.ReactToScreen,
                Actions.RequestNarration
            };

        private static DateTime LastPollTime = DateTime.MinValue;
        private static readonly Dictionary<string, string> rewardsCreated = new();

        public override string GetSystemPersona()
        {
            return string.Format(Prompts.Personas.Random(), Name);
        }

        public override async Task CleanUp()
        {
            await DeleteAllRewards();
        }
        protected override async Task AI()
        {
            var time = DateTime.Now;

            var mischief = Random.Next(3) == 0;
            if (mischief)
            {
                var actionToTake = AI_CAPABILITIES[Random.Next(AI_CAPABILITIES.Count)];
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
                        if (LastPollTime.AddMinutes(15) < time)
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
                            if (Random.Next(5) == 0)
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
                    case Actions.ReactToScreen:
                        await ReactToCurrentScreen();
                        break;
                    case Actions.RequestNarration:
                        StreamTts($"Hey {Server.Instance.Narrator.Name}, what's going on here?");
                        await Server.Instance.Narrator.ReactToCurrentScreen();
                        break;
                }
            }
            else
            {
                switch (Random.Next(3))
                {
                    case 0:
                    case 1:
                        await Chatter(); break;
                    case 2: 
                        await ReactToCurrentScreen(); break;
                }
            }

            await Task.Delay(300 * 1_000);
            return;
        }

        protected override async Task AI_On_Start()
        {
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"welcome back",
                persona: Persona
            );
            await Task.Delay(15 * 1_000);
            return;
        }

        protected override async Task AI_On_Stop()
        {
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"goodbye",
                persona: Persona
            );
            return;
        }

        public override async Task<bool> CreatePoll(string topic = "")
        {
            var time = DateTime.Now;
            if (LastPollTime.AddMinutes(5) < time)
            {
                LastPollTime = time;
            }
            else
            {
                log.Info("Poll is on cooldown!");
                return false;
            }

            string pollPrompt = Poll.PollPrompt;
            if (!string.IsNullOrWhiteSpace(topic))
            {
                pollPrompt = string.Format(Poll.PollTopicPrompt, topic);
            }
            string response = await Server.Instance.chatgpt.GetResponseText(
                persona: Persona,
                chatPrompt: pollPrompt,
                options: new(1.33, 2, 2)
            );
            log.Info(response);

            Poll poll = PollParser.parsePoll(response);
            StreamTts(Poll.GetPollAnnouncement());
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
                pollMessageBuilder.Append($"Option {x + 1}: {options[x]}\r\n");
            }

            string pollMessage = pollMessageBuilder.ToString();

            var messages = ConvertToMessages(new List<string>() {
                    pollMessage,
                    Poll.PollAnnounce
            });
            var response = await Server.Instance.chatgpt.GetResponseText(Persona, messages);
            log.Info(response);
            StreamTts(response);

            return true;
        }

        public override async Task<bool> ConcludePoll(string title, string winner)
        {
            StreamTts("The results are in...");
            var prompt = String.Format(Poll.PollEndPrompt, title, winner);
            await Server.Instance.chatgpt.GetResponse(Persona, prompt);
            return true;
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
            await Server.Instance.chatgpt.GetResponse(Persona, prompt);
            return true;
        }

        public override async Task<bool> ChangeTitle()
        {
            var currentGame = await Server.Instance.twitch.GetCurrentGame();
            var prompt = $"new title for my \"{currentGame}\" stream. limit 5 words.";
            var newTitle = await Server.Instance.chatgpt.GetResponseText(Persona, prompt);
            StreamTts($"How about a new stream title? Maybe... {newTitle}!");
            Server.Instance.twitch.ChangeTitle(newTitle);

            return true;
        }

        private async Task BanRandomUser()
        {
            var allChatters = await Server.Instance.twitch.GetChatters();
            var banned = false;

            do
            {
                var userToBan = allChatters[Random.Next(allChatters.Count)];
                if (userToBan.UserName != "CursedMoose" && userToBan.UserName != "Nightbot")
                {
                    var prompt = $"pretend you are banning user \"{userToBan.UserName}\"";
                    await Server.Instance.chatgpt.GetResponse(Persona, prompt);

                    banned = true;
                }
            } while (!banned);

            return;
        }

        public async Task<string> CreateReward()
        {
            var rewardTitle = await Server.Instance.chatgpt.GetResponseText(Persona, "create a new point reward. limit 5 words");
            var rewardCost = Random.Next(500, 10_000);
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
                var rewardToDelete = rewardsCreated.ElementAt(Random.Next(rewardsCreated.Count));
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
            StreamTts("Let's paint a picture!");

            var getPrompt = "make an image prompt. limit 5 words";
            var imagePrompt = await Server.Instance.chatgpt.GetResponseText(Persona, getPrompt);
            log.Info($"Got image prompt: {imagePrompt}");
            var image = await Server.Instance.chatgpt.GetImage(imagePrompt);
            var imageFile = await Server.Instance.file.SaveImage(image, Agent);

            var announcePrompt = $"announce your painting \"{imagePrompt}\" and describe what's in it";

            var announcement = await Server.Instance.chatgpt.GetResponseFromImagePrompt(Persona, announcePrompt, image);
            if (!string.IsNullOrEmpty(announcement))
            {
                ObsScenes.LastImage.Enable();
                log.Info($"Major announcement: {announcement}");
                PlayTts(announcement);
                Server.Instance.twitch.Respond($"\"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(imagePrompt)}\": {image}");
                Server.Instance.file.PostToWebsite(Agent, new FileGenerator.FileGenerator.Post("painting", imagePrompt, imageFile, announcement));
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

        public override async Task<int> RollDice(int diceMax = 20)
        {
            foreach (var scene in ObsScenes.AllDice)
            {
                scene.Disable();
            }
            var result = Random.Next(1, diceMax + 1);
            var diceResultScene = ObsScenes.AllDice[result - 1];
            diceResultScene.Enable();
            var text = await Server.Instance.chatgpt.GetResponseText(Persona, $"react to me rolling a {result} out of {diceMax}. limit 15 words");
            ObsScenes.DiceMain.Enable();
            StreamTts(text);
            ObsScenes.DiceMain.Disable();
            diceResultScene.Disable();

            return result;
        }

        private string GetDiceReactionString(int result)
        {
            return result switch
            {
                >= 20 => GetDiceReaction20(),
                >= 13 => $"Nice, you rolled a {result}",
                <= 1 => "Haha only a 1! What a loser!",
                <= 8 => GetDiceReactionBad(result),
                _ => $"{result}!"
            };
        }

        private string GetDiceReaction20()
        {
            var Reactions = new List<string>()
            {
                "Holy cowabunga a 20!",
                "Wow!! A 20!!!"
            };

            return Reactions[Random.Next(Reactions.Count)];
        }

        private string GetDiceReactionBad(int result)
        {
            var Reactions = new List<string>()
            {
                "Aw, you only rolled a {0}",
                "{0}. You stink.",
                "Uh oh, only a {0}"
            };

            return string.Format(Reactions[Random.Next(Reactions.Count)], result);
        }

    }
}
