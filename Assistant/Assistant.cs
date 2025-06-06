﻿using System.Diagnostics;
using System.Text;
using TwitchBot.Assistant.AI;
using TwitchBot.Assistant.Polls;
using TwitchBot.ChatGpt;
using TwitchBot.Discord;
using TwitchBot.EEG;
using TwitchBot.ElevenLabs;
using TwitchBot.EyeTracking;
using TwitchBot.OBS.Scene;
using TwitchBot.ScreenCapture;
using TwitchBot.Steam;
using TwitchBot.Twitch.Model;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using static TwitchBot.ChatGpt.ChatGpt;

namespace TwitchBot.Assistant
{
    public abstract class Assistant
    {
        public Assistant(string name, VoiceProfile voice, ObsSceneId sceneId)
        {
            Name = name;
            Voice = voice;
            Obs = sceneId;
            log = new(Name);
            Agent = new("assistant", Name);
        }

        public static readonly object TtsLock = new();

        public string Name { get; private set; }
        public VoiceProfile Voice { get; private set; }
        public ObsSceneId Obs { get; private set; }
        protected readonly Logger log;
        protected readonly Random Random = new Random();
        protected Dictionary<Actions, DateTime> LastActionTimes = new();
        public readonly FileGenerator.FileGenerator.Agent Agent;
        private bool AI_Running = false;
        protected AssistantContext<SteamContext> Context_Steam = new(SteamContext.Empty);
        public virtual string GetSystemPersona()
        {
            return Name;
        }
        public string Persona { get { return GetSystemPersona(); } }

        public virtual async void WelcomeBack(string gameTitle)
        {
            log.Info($"Oh, welcome back to {gameTitle}...");
            var welcomeBack = $"welcome me back to {gameTitle}";
            await Server.Instance.chatgpt.GetResponse(Persona, welcomeBack);
        }

        public virtual async void WelcomeFollower(string username)
        {
            await Server.Instance.chatgpt.GetResponse(Persona, $"welcome new follower \"{username}\"");
        }

        public virtual async void WelcomeSubscriber(string username, int length)
        {
            await Server.Instance.chatgpt.GetResponse(Persona, $"thank \"{username}\" for subscribing for {length} months");
        }

        public abstract Task<bool> ChangeTitle();
        public abstract Task<bool> CreatePoll(string topic);

        public virtual async Task<bool> AnnouncePoll(string title, List<string> options)
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

        public virtual async Task<bool> ConcludePoll(string title, string winner)
        {
            StreamTts("The results are in...");
            var prompt = String.Format(Poll.PollEndPrompt, title, winner);
            await Server.Instance.chatgpt.GetResponse(Persona, prompt);
            return true;
        }

        public virtual async Task<bool> AnnouncePrediction(ChannelPredictionBegin prediction)
        {
            StringBuilder predictionMessageBuilder = new();
            predictionMessageBuilder.Append($"Title:{prediction.Title}\r\n");
            for (int x = 0; x < prediction.Outcomes.Length; x++)
            {
                predictionMessageBuilder.Append($"Option {x + 1}: {prediction.Outcomes[x].Title}\r\n");
            }

            string pollMessage = predictionMessageBuilder.ToString();

            var messages = ConvertToMessages(new List<string>() {
                    pollMessage,
                    "announce the prediction. limit 25 words",
            });
            var response = await Server.Instance.chatgpt.GetResponseText(Persona, messages);
            StreamTts(response);

            return true;
        }

        public virtual async Task<bool> AnnouncePredictionLocked(ChannelPredictionLock prediction)
        {
            string message = $"announce that voting is closed for \"{prediction.Title}\"";
            await Server.Instance.chatgpt.GetResponse(Persona, message);
            return true;
        }

        public virtual async Task<bool> ConcludePrediction(ChannelPredictionEnd predictionResult)
        {
            var winningOutcome = predictionResult.Outcomes.Where(outcome => outcome.Id == predictionResult.WinningOutcomeId).First();
            var totalPointsWagered = predictionResult.Outcomes.Sum(outcome => outcome.ChannelPoints);
            var prompt = $"the prediction \"{predictionResult.Title}\" ended. \"{winningOutcome.Title}\" was the result. ";
            await Server.Instance.chatgpt.GetResponse(Persona, prompt);
            return true;
        }

        public virtual async Task<bool> ChannelRewardClaimed(string byUsername, string rewardTitle, int cost)
        {
            var prompt = $"react to \"{byUsername}\" redeeming channel reward \"{rewardTitle}\"";
            await Server.Instance.chatgpt.GetResponse(Persona, prompt);
            return true;
        }

        public virtual async Task<bool> AnnounceAd(int adSeconds = 5)
        {
            var run = $"announce that it is time for {adSeconds} seconds of ads";
            await Server.Instance.chatgpt.GetResponse(Persona, run);
            ObsScenes.Ads.Enable();
            await Task.Delay((adSeconds + 10) * 1000);
            ObsScenes.Ads.Disable();
            var end = "announce that the ad is over";
            await Server.Instance.chatgpt.GetResponse(Persona, end);

            return true;
        }
        public virtual async Task<int> RollDice(int diceMax = 20)
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

        public abstract Task CleanUp();

        public void PlayTts(string message)
        {
            var cleanedMessage = Server.Instance.elevenlabs.CleanStringForTts(message);
            var elevenLabsResponse = Server.Instance.elevenlabs.MakeTtsRequest(cleanedMessage, Voice);
            if (elevenLabsResponse.IsSuccessStatusCode)
            {
                lock (TtsLock)
                {
                    Obs.Enable();
                    using Stream responseStream = elevenLabsResponse.Content.ReadAsStream();
                    TtsPlayer.PlayResponseStream(responseStream);
                    Obs.Disable();
                }
            }
        }

        public async Task WaitForSilence()
        {
            while (Server.Instance.speech.IsTalking())
            {
                log.Debug("Waiting for silence...");
                await Task.Delay(250);
            }
        }

        public void StreamTts(string message)
        {
            var cleanedMessage = Server.Instance.elevenlabs.CleanStringForTts(message);
            Server.Instance.elevenlabs.StreamTts(Voice, cleanedMessage, Obs);
        }

        public async Task RespondToPrompt(string prompt)
        {
            var options = new ChatGptOptions(1.33, 2, 2);
            await Server.Instance.chatgpt.GetResponse(Persona, prompt, options);
            return;
        }

        public async Task Chatter()
        {
            var options = new ChatGptOptions(1.33, 2, 2);
            await Server.Instance.chatgpt.GetResponse(Persona, "say something interesting", options);
            return;
        }

        public async void ReactToGameState(string gameState)
        {
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"react to me {gameState}",
                persona: Persona
            );
        }
        public async void ReactToImage(string imageUrl, string prompt = "give exciting commentary on this image")
        {
            var reaction = await Server.Instance.chatgpt.GetResponseFromImagePrompt(Persona, prompt, imageUrl);
            StreamTts(reaction);
        }

        public async void ReactToGameStateAndCurrentScreen(string gameState)
        {
            var fileUrl = await Server.Instance.TakeAndUploadScreenshot();
            log.Info($"Reacting to {gameState}");
            ReactToImage(fileUrl, $"give exciting commentary on me {gameState}");
        }

        public async Task ReactToNewAchievement(string gameName, string achievementName, string description)
        {
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"congratulate me for completing the achievement \"{achievementName}\" in {gameName} for {description}",
                persona: Persona
            );
        }

        public async Task ReactToCurrentScreen()
        {
            await ReactToCurrentScreen("");
        }

        public async Task ReactToCurrentScreen(string prompt = "")
        {
            var reactionPrompt = prompt;

            if (string.IsNullOrEmpty(prompt))
            {
                reactionPrompt = Prompts.Reactions.Random();
            }

            if (DiscordBot.IsEnabled())
            {
                var fileUrl = await Server.Instance.TakeAndUploadScreenshot();
                ReactToImage(fileUrl, reactionPrompt);
            }
            else
            {
                log.Error("Could not react to current screen as discord is disabled.");
            }
        }

        public async Task LookAtWhatISee(string prompt = "")
        {
            if (DiscordBot.IsEnabled() && TobiiEyeTracker.IsEnabled())
            {
                var reactionPrompt = prompt;
                if (string.IsNullOrEmpty(prompt))
                {
                    reactionPrompt = Prompts.Reactions.Random();

                    if (MuseMonitor.IsEnabled()) 
                    { 
                        var currentBrain = Server.Instance.brain.CurrentBrainWaveState();
                        reactionPrompt = $"{reactionPrompt}. I am currently feeling {currentBrain}.";
                    }
                }
                var img = Server.Instance.eyetracker.CaptureLatestVisionArea();
                var fileUrl = await Server.Instance.UploadImage(img);
                ReactToImage(fileUrl, reactionPrompt);
            }
            else
            {
                log.Error("Could not react to current mental state as discord is disabled.");
            }
        }

        public async Task RespondToChannelUpdate(ChannelInfo oldInfo, ChannelInfo newInfo)
        {
            if (oldInfo.GameName != newInfo.GameName)
            {
                await RespondToPrompt($"I've changed from playing {oldInfo.GameName} to playing {newInfo.GameName}");
            }
            else if (oldInfo.Title != newInfo.Title)
            {
                await RespondToPrompt($"I've changed the title from {oldInfo.Title} to {newInfo.Title}");
            }


            return;
        }

        protected virtual async Task AI()
        {
            log.Info("Incorrectly using this AI");
            await Task.Delay(300_000);
        }

        private async Task UpdateContext()
        {
            do
            {
                log.Info("Updating Context");
                Context_Steam.Update(await Server.Instance.steam.GetCurrentSteamContext());
                await Context_OnUpdate();
                await Task.Delay(60_000);
            } while (AI_Running);
        }

        protected virtual async Task Context_OnUpdate()
        {
            log.Info("Incorrectly using this Context");
            await Task.Delay(300_000);
            return;
        }

        protected virtual async Task AI_On_Start()
        {
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"welcome back",
                persona: Persona
            );
            await Task.Delay(15 * 1_000);
            return;
        }

        protected virtual async Task AI_On_Stop()
        {
            await CleanUp();
            await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"goodbye",
                persona: Persona
            );
            return;
        }
        private async Task Run_AI()
        {
            do {
                await AI();
            } while (AI_Running);

            return;
        }

        public async Task StartAI()
        {
            if (!AI_Running)
            {
                log.Info($"Hello at {DateTime.Now}");
                await AI_On_Start();
                AI_Running = true;
                Task.Run(Run_AI);
                Task.Run(UpdateContext);
            }
            return;
        }

        public bool IsRunning()
        {
            return AI_Running;
        }

        public async Task StopAI()
        {
            log.Info($"Goodbye at {DateTime.Now}");
            AI_Running = false;
            await AI_On_Stop();
            return;
        }

        public async Task Commemorate(string excitingEvent, TwitchUser requester, ICollection<TwitchUser> commemorators)
        {
            var image = await Server.Instance.chatgpt.GetImage(excitingEvent, requester);
            var agent = new FileGenerator.FileGenerator.Agent("user", requester.UserName);
            var imageFile = await Server.Instance.file.SaveImage(image, agent);
            var headline = $"{requester.UserName} and {commemorators.Count} others commemorate {excitingEvent}";

            if (image != null)
            {
                ObsScenes.LastImage.Enable();
            }

            var commentary = await Server.Instance.chatgpt.GetResponse(
                chatPrompt: $"commemorate {excitingEvent} with {requester.UserName} and {commemorators.Count} others",
                persona: Persona
            );

            Server.Instance.file.PostToWebsite(agent, new FileGenerator.FileGenerator.Post("commemoration", excitingEvent, imageFile, commentary));
            ObsScenes.LastImage.Disable();

            return;
        }

        public async Task ReadImage(string filePath = ImageFiles.Region)
        {
            var stopwatch = Stopwatch.StartNew();
            var text = await Server.Instance.imageText.ReadText(filePath);
            stopwatch.Stop();
            log.Info($"Model time: {stopwatch.ElapsedMilliseconds}ms");
            StreamTts(text);
        }

        public async Task ReadImage(Bitmap image)
        {
            var stopwatch = Stopwatch.StartNew();
            var text = await Server.Instance.imageText.ReadText(image);
            stopwatch.Stop();
            log.Info($"Model time: {stopwatch.ElapsedMilliseconds}ms");
            StreamTts(text);
        }

        public async void AskAnotherAssistant(Assistant otherAssisant, string prompt)
        {
            var options = new ChatGptOptions(1.33, 2, 2);
            await Server.Instance.chatgpt.GetResponse(otherAssisant.Name, "prompt", options);
        }
    }
}
