using System.Globalization;
using TwitchBot.Assistant.AI;
using TwitchBot.Assistant.Polls;
using TwitchBot.ElevenLabs;
using TwitchBot.OBS.Scene;

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
            Mischief[Actions.RunPoll] = TryRunPoll;
            Mischief[Actions.CreateReward] = TryCreateReward;
            Mischief[Actions.PaintPicture] = PaintPicture;
            Mischief[Actions.ReactToScreen] = ReactToCurrentScreen;
            Mischief[Actions.RequestNarration] = async () =>
            {
                StreamTts($"Hey {Server.Instance.Narrator.Name}, what's going on here?");
                await Server.Instance.Narrator.ReactToCurrentScreen();
            };

            Mayhem[Actions.None] = () => { return Task.CompletedTask; };
            Mayhem[Actions.Chat] = Chatter;
            Mayhem[Actions.ReactToScreen] = ReactToCurrentScreen;
        }

        private static DateTime LastPollTime = DateTime.MinValue;
        private static readonly Dictionary<string, string> rewardsCreated = new();
        public Dictionary<Actions, Func<Task>> Mischief = new();
        public Dictionary<Actions, Func<Task>> Mayhem = new();

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
                try
                {
                    var actionToTake = Mischief.ElementAt(Random.Next(Mischief.Count));
                    log.Info($"Mischief!: {actionToTake.Key}");
                    await actionToTake.Value();
                    LastActionTimes[actionToTake.Key] = DateTime.Now;
                }
                catch(Exception ex)
                {
                    log.Error($"Could not do Mischief because of {ex.Message}");
                }
            }
            else
            {
                try
                {
                    var actionToTake = Mayhem.ElementAt(Random.Next(Mayhem.Count));
                    log.Info($"Mayhem!: {actionToTake.Key}");
                    await actionToTake.Value();
                    LastActionTimes[actionToTake.Key] = DateTime.Now;
                }
                catch (Exception ex)
                {
                    log.Error($"Could not do Mayhem because of {ex.Message}");

                }

            }

            await Task.Delay(300 * 1_000);
            return;
        }

        private async Task TryRunPoll()
        {
            DateTime lastPollTime = DateTime.MinValue;
            LastActionTimes.TryGetValue(Actions.RunPoll, out lastPollTime);

            if (lastPollTime.AddMinutes(15) < DateTime.Now)
            {
                await CreatePoll();
            }
            else
            {
                log.Info("Poll is on cooldown!");
            }
        }

        private async Task TryCreateReward()
        {
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
        }

        public async Task<bool> CreatePoll()
        {
            return await CreatePoll("");
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
                StreamTts(announcement);
                Server.Instance.twitch.Respond($"\"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(imagePrompt)}\": {image}");
                Server.Instance.file.PostToWebsite(Agent, new FileGenerator.FileGenerator.Post("painting", imagePrompt, imageFile, announcement));
            }
            ObsScenes.LastImage.Disable();

        }

        public override async Task<bool> AnnounceAd(int adSeconds = 5)
        {
            var run = "apologize that it is time for an ad";
            await Server.Instance.chatgpt.GetResponse(Persona, run);
            ObsScenes.Ads.Enable();
            await Task.Delay((adSeconds + 10) * 1000);
            ObsScenes.Ads.Disable();
            var end = "rejoice that the ad is over";
            await Server.Instance.chatgpt.GetResponse(Persona, end);

            return true;
        }
    }
}
