using TwitchBot.Twitch.Commands;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix;
using TwitchLib.Api.Helix.Models.Polls.CreatePoll;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.Handler;
using System.ComponentModel.Design;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.Models.Polls;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using TwitchLib.EventSub.Websockets.Client;
using TwitchLib.EventSub.Websockets.Handler.Channel.ChannelPoints.Redemptions;
using TwitchLib.EventSub.Websockets.Handler.Channel.Polls;
using TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation;
using static TwitchBot.Config.TwitchConfig;
using System.Runtime.Caching;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.Ads;
using TwitchLib.Api.Helix.Models.Predictions.CreatePrediction;
using TwitchLib.Api.Helix.Models.Predictions;
using OutcomeOption = TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome;
using TwitchLib.EventSub.Websockets.Handler.Channel.Predictions;
using TwitchBot.Twitch.NotificationHandlers;
using TwitchBot.Twitch.Model;
using TwitchLib.EventSub.Websockets.Handler.Channel;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;

namespace TwitchBot.Twitch
{
    public class TwitchIrcBot
    {
        public readonly bool Enabled = false;
        readonly TwitchClient client;
        readonly TwitchAPI api;
        readonly EventSubWebsocketClient events;
        readonly WebsocketClient EventSubSocket = new();
        readonly TwitchTts tts;

        Helix API { get { return api.Helix; } }
        Auth Auth { get { return api.Auth; } }

        public ChannelInfo CurrentChannelInfo = new();
        public Prediction? CurrentPrediction = null;
        public Commemoration? CurrentCommemoration = null;

        internal readonly List<CommandHandler> commands;
        readonly MemoryCache eventLog = new("Events");
        readonly Logger log = new("Twitch");
        public TwitchIrcBot(bool enabled = true)
        {
            Enabled = enabled;
            commands = new List<CommandHandler>()
            {
                new GetCommands(),
                new BotCheck(),
                new ElevenLabsUsage(),
                new CommemerateEvent(),
                new CreatePoll(),
                new SetVoice(),
                new RollDice(),
            };

            tts = new();
            api = Init_API();
            events = Init_EventSub();
            client = Init_IRC_Client();

            var scopes = new List<string>()
            {
                "moderator:manage:banned_users",
                "moderator:read:chatters",
                "moderator:read:followers",
                "channel:manage:polls",
                "channel:manage:predictions",
                "channel:manage:redemptions",
                "channel:manage:broadcast",
                "channel:read:ads",
                "channel:edit:commercial"
            };

            if (enabled)
            {
                Task.Delay(1000);
                client?.Connect();
                StartApi(scopes).GetAwaiter().GetResult();
                events?.ConnectAsync();
                CurrentChannelInfo = ChannelInfo.FromChannelInformation(GetStreamInfo().GetAwaiter().GetResult());
            }
        }

        public async void HealthCheck()
        {

            log.Info($"[Client] Initialized: {client.IsInitialized} | Connected: {client.IsConnected}");
            log.Info($"[Events] Connected: {EventSubSocket.IsConnected}");
            if (!Enabled) {
                log.Info($"Service is Disabled");
                return;
            }

            log.Info($"[API] Valid: {Auth.ValidateAccessTokenAsync().Result.Login}");
            var subs = await API.EventSub.GetEventSubSubscriptionsAsync(status: "enabled");
            log.Info($"[Events] Subscriptions: {subs.Subscriptions.Length} | Cost: {subs.TotalCost}");

            foreach (var sub in subs.Subscriptions)
            {
                foreach (var condition in sub.Condition)
                {
                    log.Info($"[Events]\t[{sub.Type}]: {condition.Key}={condition.Value}");
                }
                log.Info($"[Events]\t[{sub.Type}]: {sub.Status} ({sub.Cost})");
                log.Info($"[Events]\t[{sub.Type}]: {sub.CreatedAt}");
            }
        }
        private TwitchAPI Init_API()
        {
            TwitchAPI api = new();
            api.Settings.ClientId = Secrets.API_CLIENT_ID;
            api.Settings.Scopes = new List<AuthScopes>() {
                AuthScopes.Helix_Moderator_Read_Chatters,
                AuthScopes.Helix_Moderator_Read_Followers,
                AuthScopes.Helix_Channel_Manage_Polls,
                AuthScopes.Helix_Channel_Manage_Predictions,
                AuthScopes.Helix_Channel_Manage_Redemptions,
                AuthScopes.Channel_Commercial,
                AuthScopes.Helix_Channel_Edit_Commercial
            };
            return api;
        }

        private TwitchClient Init_IRC_Client()
        {
            ConnectionCredentials credentials = new(
                twitchUsername: AccountInfo.ACCOUNT_NAME,
                twitchOAuth: Secrets.OAUTH_PASSWORD);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            WebSocketClient customClient = new(clientOptions);
            var client = new TwitchClient(customClient);
            client.Initialize(credentials, AccountInfo.CHANNEL);

            client.OnLog += Client_OnLog;
            client.OnJoinedChannel += Client_OnJoinedChannel;
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnWhisperReceived += Client_OnWhisperReceived;
            client.OnNewSubscriber += Client_OnNewSubscriber;
            client.OnReSubscriber += Client_OnReSubscriber;
            client.OnConnected += Client_OnConnected;
            return client;
        }

        private EventSubWebsocketClient Init_EventSub()
        {
            ServiceContainer sp = new();
            sp.AddService(EventSubSocket.GetType(), EventSubSocket);
            ILogger<EventSubWebsocketClient> logger = new NullLogger<EventSubWebsocketClient>();
            List<INotificationHandler> handlers = new()
            {
                new ChannelUpdateHandler(),
                new ChannelPointsCustomRewardRedemptionAddHandler(),
                new ChannelPollBeginHandler(),
                new ChannelPollEndHandler(),
                new ChannelPredictionBeginHandler(),
                new ChannelPredictionLockBeginHandler(),
                new ChannelPredictionEndHandler(),
                new ChannelAdBreakBeginHandler(),
            };

            EventSubWebsocketClient events = new(logger, handlers, sp, EventSubSocket);
            events.WebsocketConnected += EventSub_OnConnected;
            events.WebsocketDisconnected += EventSub_OnDisconnected;
            events.WebsocketReconnected += EventSub_OnReconnect;
            events.ErrorOccurred += EventSub_Error;

            events.ChannelAdBreakBegin += EventSub_OnAdBreakBegin;

            events.ChannelFollow += EventSub_OnChannelFollow;
            events.ChannelPollBegin += EventSub_OnPollBegin;
            events.ChannelPollEnd += EventSub_OnPollEnd;

            events.ChannelPointsCustomRewardRedemptionAdd += EventSub_OnChannelPointsRedeemed;

            events.ChannelPredictionBegin += EventSub_OnPredictionStarted;
            events.ChannelPredictionLock += EventSub_OnPredictionLocked;
            events.ChannelPredictionEnd += EventSub_OnPredictionEnded;

            events.ChannelUpdate += EventSub_OnChannelUpdate;

            log.Info("Event sub initialized.");
            return events;
        }

        private async Task StartApi(List<string> scopes)
        {
            var server = new AuthServer(Secrets.API_REDIRECT_URL);
            var codeUrl = AuthServer.GetAuthorizationCodeUrl(Secrets.API_CLIENT_ID, Secrets.API_REDIRECT_URL, scopes);
            Console.WriteLine($"Please authorize here:\n{codeUrl}");
            System.Diagnostics.Process.Start(@"C:\Program Files\Mozilla Firefox\firefox.exe", codeUrl);
            var auth = await server.Listen();
            var resp = await Auth.GetAccessTokenFromCodeAsync(auth?.Code, Secrets.API_CLIENT_SECRET, Secrets.API_REDIRECT_URL);
            api.Settings.AccessToken = resp.AccessToken;
            var user = (await API.Users.GetUsersAsync()).Users[0];
            Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName}\nScopes: {string.Join(", ", resp.Scopes)}");
        }

        public void RespondTo(ChatMessage message, string withMessage)
        {
            client.SendMessage(message.Channel, withMessage);
        }

        public void RespondTo(TwitchUser user, string withMessage)
        {
            client.SendMessage(user.Channel, withMessage);
        }

        public void Respond(string withMessage)
        {
            if (Enabled)
            {
                log.Info($"Sending {withMessage} to {AccountInfo.CHANNEL}");
                client.SendMessage(AccountInfo.CHANNEL, withMessage);
            }
        }
        public void Stop()
        {
            client.Disconnect();
            events.DisconnectAsync();
            log.Info("TwitchBot Disconnected.");
        }

        #region TwitchClient Handlers
        private void Client_OnLog(object? sender, OnLogArgs e)
        {
            // log.Info($"{e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnected(object? sender, OnConnectedArgs e)
        {
            log.Info($"Connected to {e.AutoJoinChannel}");
        }

        private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            log.Info("Bot has joined channel.");
        }

        private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Message.StartsWith('!'))
            {
                log.Info($"Processing bot command {e.ChatMessage.Message.Split(" ")[0]} from {e.ChatMessage.Username}");
                HandleCommand(sender, e);
                return;
            }

            log.Info($"{e.ChatMessage.Username} sent message: ${e.ChatMessage.Message}");

            if (Permissions.IsUserInGroup(e.ChatMessage.Username, PermissionGroup.Admin) && e.ChatMessage.CustomRewardId == null)
            {
                PlayTts(sender, e);
            }

            if (!Permissions.IsUserInGroup(e.ChatMessage.Username, PermissionGroup.Admin)) // lol admins can't get banned 
            {
                if (e.ChatMessage.Message.Contains("dogehype") || e.ChatMessage.Message.Contains("dot com"))
                {
                    client.TimeoutUser(e.ChatMessage.Channel, e.ChatMessage.Username, TimeSpan.FromMinutes(1), "Looks like you're dodging links.");
                }
            }
        }

        private void Client_OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
        {
            if (e.WhisperMessage.Username == "my_friend")
            {
                client.SendWhisper(e.WhisperMessage.Username, "Hey! Whispers are so cool!!");
            }
        }

        private void Client_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
        {
            Server.Instance.Assistant.WelcomeSubscriber(e.Subscriber.DisplayName, 1);
        }

        private void Client_OnReSubscriber(object? sender, OnReSubscriberArgs e)
        {
            Server.Instance.Assistant.WelcomeSubscriber(e.ReSubscriber.DisplayName, e.ReSubscriber.Months);
        }

        #endregion TwitchClient Handlers

        private void HandleCommand(object? sender, OnMessageReceivedArgs e)
        {
            foreach (CommandHandler handler in commands)
            {
                if (handler.CanHandle(e.ChatMessage))
                {
                    handler.Handle(this, e.ChatMessage);
                    return;
                }
            }

            log.Info($"No handler found for {e.ChatMessage.Message}.");
            return;
        }

        #region Tts
        private void PlayTts(object? sender, OnMessageReceivedArgs e)
        {
            if (Enabled)
            {
                tts.Play(e.ChatMessage);
            }
        }

        private void PlayRumorTts(object? sender, OnMessageReceivedArgs e)
        {
            if (Enabled)
            {
                tts.PlayRumor(e.ChatMessage.Message);
            }
        }

        private void PlayRumorTts(string message)
        {
            if (Enabled)
            {
                tts.PlayRumor(message);
            }
        }
        #endregion Tts

        #region API Hooks
        public async Task<List<string>> GetChatterNames()
        {
            var allChatters = await GetChatters();
            return allChatters.Select(chatter => chatter.UserName).ToList();
        }

        public async Task<List<Chatter>> GetChatters()
        {
            if (!client.IsConnected)
            {
                return new();
            }

            var page = "";
            List<Chatter> allChatters = new();
            do
            {
                var chatters = await API.Chat.GetChattersAsync(AccountInfo.USER_ID, AccountInfo.USER_ID, 1000, page);
                page = chatters.Pagination.Cursor;
                allChatters.AddRange(chatters.Data);
            } while (page != null);

            return allChatters;
        }

        public async Task<bool> CreatePoll(string title, List<string> choices)
        {
            if (!Enabled) { return false; }

            if (!client.IsConnected || string.IsNullOrWhiteSpace(title) || choices.Count <= 0)
            {
                log.Info($"Client: {client.IsConnected} | Title: {title} | Choices: {choices.Count}");
                return false;
            }

            var request = new CreatePollRequest
            {
                DurationSeconds = 60,
                BroadcasterId = AccountInfo.USER_ID,
                Title = title
            };

            List<Choice> realChoices = new();
            foreach (string choice in choices)
            {
                var choiceObj = new Choice
                {
                    Title = choice
                };
                realChoices.Add(choiceObj);
            }

            request.Choices = realChoices.ToArray();

            log.Info("Creating Poll...");
            try
            {
                await API.Polls.CreatePollAsync(request);
                return true;
            } catch (Exception ex)
            {
                log.Error($"Could not run poll due to {ex.Message}");
                return false;
            }
        }

        public async void ChangeTitle(string newTitle)
        {
            if (!Enabled) { return; }
            ModifyChannelInformationRequest request = new()
            {
                Title = newTitle + " | !hellbot"
            };
            await API.Channels.ModifyChannelInformationAsync(broadcasterId: AccountInfo.USER_ID, request: request);
        }

        public async Task<bool> ChangeGame(string newGame)
        {
            if (!Enabled) { return false; }
            var sanitizedName = newGame.Replace("Demo", "").Trim();

            ModifyChannelInformationRequest request = new();
            var gameInfo = await API.Games.GetGamesAsync(gameNames: new List<string>() { sanitizedName });
            if (gameInfo?.Games == null || gameInfo.Games.Length <= 0)
            {
                log.Info($"Couldn't find ID for game: {sanitizedName}");
                return false;
            }
            var gameId = gameInfo.Games[0].Id;
            log.Info(gameId);
            request.GameId = gameId;
            await API.Channels.ModifyChannelInformationAsync(broadcasterId: AccountInfo.USER_ID, request: request);
            return true;
        }

        public async Task<ChannelInformation> GetStreamInfo()
        {
            if (Enabled)
            {
                var info = await API.Channels.GetChannelInformationAsync(AccountInfo.CHANNEL_ID);
                return info.Data[0];
            }
            else
            {
                return new ChannelInformation();
            }
        }

        public async Task<string> GetCurrentGame()
        {
            var info = await GetStreamInfo();
            return info.GameName;
        }

        public async Task<string> GetCurrentTitle()
        {
            var info = await GetStreamInfo();
            return info.Title;
        }

        public async Task<bool> BanUser(string username, string reason, int timeoutSeconds)
        {
            var request = new BanUserRequest
            {
                UserId = username,
                Duration = timeoutSeconds,
                Reason = reason
            };

            await API.Moderation.BanUserAsync(
                broadcasterId: AccountInfo.USER_ID,
                moderatorId: AccountInfo.USER_ID,
                banUserRequest: request
            );

            return true;
        }

        public async Task<string> CreateCustomReward(string rewardName, int rewardCost, string? prompt = null)
        {
            var request = new CreateCustomRewardsRequest()
            {
                Title = rewardName,
                Cost = rewardCost,
                ShouldRedemptionsSkipRequestQueue = true,
                Prompt = prompt,
                IsEnabled = true,
            };

            var reward = await API.ChannelPoints.CreateCustomRewardsAsync(
                broadcasterId: AccountInfo.USER_ID,
                request: request
            );

            return reward.Data[0].Id;
        }

        public async Task<bool> DeleteCustomReward(string rewardId)
        {
            try
            {
                await API.ChannelPoints.DeleteCustomRewardAsync(
                    broadcasterId: AccountInfo.USER_ID,
                    rewardId: rewardId
                );
            }
            catch (Exception ex)
            {
                log.Info($"Couldn't delete reward {rewardId} due to {ex.Message}");
                return false;
            }

            return true;


        }

        public async Task<bool> RunAd(int adLength = 5)
        {
            if (Enabled)
            {
                StartCommercialRequest request = new()
                {
                    BroadcasterId = AccountInfo.USER_ID,
                    Length = adLength
                };

                try
                {
                    var response = await API.Ads.StartCommercialAsync(request);
                    return true;
                }
                catch (Exception e)
                {
                    log.Info($"Could not play ad: {e.Message}");
                    return false;
                }
            }
            return false;
        }

        public async Task<bool> CreatePrediction(string title, List<string> choices)
        {
            if (!Enabled) { return false; }

            if (!client.IsConnected || string.IsNullOrWhiteSpace(title) || choices.Count <= 0)
            {
                log.Info($"Client: {client.IsConnected} | Title: {title} | Choices: {choices.Count}");
                return false;
            }

            List<OutcomeOption> outcomes = new();
            foreach (string choice in choices)
            {
                outcomes.Add(new() { Title = choice });
            }

            var request = new CreatePredictionRequest
            {
                BroadcasterId = AccountInfo.USER_ID,
                PredictionWindowSeconds = 60,
                Title = title,
                Outcomes = outcomes.ToArray()
            };

            log.Info("Creating Prediction...");
            try
            {
                var response = await API.Predictions.CreatePredictionAsync(request);
                CurrentPrediction = response.Data[0];
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Could not run prediction due to {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ResolvePrediction(int predictionOptionWinner)
        {
            if (!Enabled) { return false; }
            if (CurrentPrediction == null) { return false; }

            var winningOutcome = CurrentPrediction.Outcomes[predictionOptionWinner];

            log.Info("Ending Prediction...");
            try
            {
                await API.Predictions.EndPredictionAsync(
                    broadcasterId: AccountInfo.USER_ID,
                    id: CurrentPrediction.Id,
                    status: PredictionEndStatus.RESOLVED,
                    winningOutcomeId: winningOutcome.Id
                );

                CurrentPrediction = null;
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Could not run prediction due to {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateRedemptionStatus(string rewardId, string redemptionId, bool completed)
        {
            if (!Enabled) { return false; }

            var newStatus = completed ? CustomRewardRedemptionStatus.FULFILLED : CustomRewardRedemptionStatus.CANCELED;
            var redemptionIds = new List<string>() { redemptionId };

            try
            {
                var response = await API.ChannelPoints.UpdateRedemptionStatusAsync(
                    broadcasterId: AccountInfo.USER_ID,
                    rewardId: rewardId,
                    redemptionIds: redemptionIds,
                    request: new UpdateCustomRewardRedemptionStatusRequest
                    {
                        Status = newStatus,
                    }
                );
                log.Info($"{response.Data}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Could not run update reward due to {ex.Message}");
                return false;
            }
        }

        public async Task<bool> Commemorate(string organizer, string occasion)
        {
            CurrentCommemoration = new(occasion, new(organizer, AccountInfo.CHANNEL));
            var started = await StartCommemoration();
            var ended = false;
            if (started)
            {
                await Task.Delay(90 * 1_000);
                ended = await EndCommemoration();
                if (!ended)
                {
                    log.Error($"Couldn't stop commemoration of {CurrentCommemoration.Event}");
                }
            }

            return started && ended;
        }

        public async Task<bool> StartCommemoration()
        {
            if (CurrentCommemoration != null)
            {
                CurrentCommemoration.Start();
                client.SendMessage(AccountInfo.CHANNEL, $"{CurrentCommemoration.Organizer.UserName} is commemorating \"{CurrentCommemoration.Event}\"! Join in the ceremony by typing !commemorate");
                return true;
            }

            return false;
        }

        public async Task<bool> EndCommemoration()
        {
            if (CurrentCommemoration != null && CurrentCommemoration.InProgress())
            {
                client.SendMessage(AccountInfo.CHANNEL, $"{CurrentCommemoration.Organizer.UserName}'s commemoration of \"{CurrentCommemoration.Event}\" is over!");
                CurrentCommemoration.Stop();
                await Server.Instance.Assistant.Commemorate(CurrentCommemoration.Event, CurrentCommemoration.Organizer, CurrentCommemoration.Observers);
                CurrentCommemoration = null;
                return true;
            }

            return false;
        }

        #endregion API Hooks

        #region EventSub Handlers
        private async Task EventSub_OnConnected(object? sender, WebsocketConnectedArgs e)
        {
            log.Info("EventSub Connected. ~~~~~~~~~~~~~~~");
            if (!e.IsRequestedReconnect)
            {
                var accessToken = await Auth.GetAccessTokenAsync();
                var valid = await Auth.ValidateAccessTokenAsync(accessToken);
                log.Info($"Token is valid for {valid.UserId}");
                log.Info($"SessionId is {events.SessionId}");

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.follow",
                    version: "2",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                        { "moderator_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.update",
                    version: "2",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.poll.begin",
                    version: "1",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.poll.end",
                    version: "1",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.channel_points_custom_reward_redemption.add",
                    version: "1",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.prediction.begin",
                    version: "1",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.prediction.lock",
                    version: "1",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.prediction.end",
                    version: "1",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                await API.EventSub.CreateEventSubSubscriptionAsync(
                    type: "channel.ad_break.begin",
                    version: "1",
                    condition: new Dictionary<string, string>()
                    {
                        { "broadcaster_user_id", AccountInfo.USER_ID },
                    },
                    method: EventSubTransportMethod.Websocket,
                    websocketSessionId: events.SessionId
                );

                var subscribedEvents = await API.EventSub.GetEventSubSubscriptionsAsync();
                foreach(var subbedEvent in subscribedEvents.Subscriptions)
                {
                    log.Info($"Subscribed to {subbedEvent.Type} ({subbedEvent.Cost})");
                }
                log.Info($"Total: {subscribedEvents.Total} ({subscribedEvents.TotalCost})");
            }
        }

        private async Task EventSub_OnDisconnected(object? sender, EventArgs e)
        {
            log.Info($"EventSub Disconnected. ~~~~~~~~~~~~~~~ Retry?");
            while (!await events.ReconnectAsync())
            {
                log.Error("Websocket reconnect failed!");
                await Task.Delay(1000);
            }
        }

        private Task EventSub_OnReconnect(object? sender, EventArgs e)
        {
            log.Info($"Websocket {events.SessionId} reconnected");
            return Task.CompletedTask;
        }
        private Task EventSub_OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
            var eventData = e.Notification.Payload.Event;
            log.Info($"{eventData.UserName} followed {eventData.BroadcasterUserName} at {eventData.FollowedAt}");
            Server.Instance.Assistant.WelcomeFollower(eventData.UserName);
            return Task.CompletedTask;
        }

        private async Task EventSub_OnAdBreakBegin(object? sender, ChannelAdBreakBeginArgs e)
        {
            var adInfo = e.Notification.Payload.Event;
            log.Info($"{adInfo.DurationSeconds}s Ad break has begun. ");
            await Server.Instance.Assistant.AnnounceAd(adInfo.DurationSeconds);
            return;
        }


        private async Task EventSub_OnPollBegin(object? sender, ChannelPollBeginArgs e)
        {
            var eventData = e.Notification.Payload.Event;
            log.Info($"Poll started: {eventData.Title}");
            log.Info($"{eventData.Choices[0].Title}");
            await Server.Instance.Assistant.AnnouncePoll(eventData.Title, eventData.Choices.Select(choice => choice.Title).ToList());
            return;
        }

        private async Task EventSub_OnPollEnd(object? sender, ChannelPollEndArgs e)
        {
            var eventData = e.Notification.Payload.Event;

            if (eventLog[eventData.Id] != null) { return; }
            eventLog.Add(eventData.Id, "end", DateTime.Now.AddMinutes(5));

            log.Info($"Poll ended: {eventData.Title}");
            log.Info("Results:");
            foreach (PollChoice choice in eventData.Choices.ToList())
            {
                log.Info($"{choice.Title}: {choice.Votes}");
            }
            var winner = eventData.Choices.MaxBy(it => it.Votes)?.Title ?? "none of the above";
            await Server.Instance.Assistant.ConcludePoll(eventData.Title, winner);
            return;
        }

        private async Task EventSub_OnChannelPointsRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            var eventData = e.Notification.Payload.Event;
            log.Info($"{eventData.UserName} redeemed \"{eventData.Reward.Title}\": {eventData.UserInput}");

            if (eventData.Reward.Title == "Start a Rumor")
            {
                PlayRumorTts(eventData.UserInput);
            }
            else if (eventData.Reward.Title == "Commemorate")
            {
                log.Info($"{eventData.UserName} is commemorating {eventData.UserInput}");

                CurrentCommemoration = new(eventData.UserInput, new(eventData.UserName, AccountInfo.CHANNEL));
                CurrentCommemoration.Start();
                client.SendMessage(AccountInfo.CHANNEL, $"{eventData.UserName} is commemorating \"{eventData.UserInput}\"! Join in the ceremony by typing !commemorate");
                await Task.Delay(90 * 1_000);
                client.SendMessage(AccountInfo.CHANNEL, $"{eventData.UserName}'s commemoration of \"{eventData.UserInput}\" is over!");
                CurrentCommemoration.Stop();
                await Server.Instance.Assistant.Commemorate(CurrentCommemoration.Event, CurrentCommemoration.Organizer, CurrentCommemoration.Observers);
                CurrentCommemoration = null;
            }
            else
            {
                await Server.Instance.Assistant.ChannelRewardClaimed(eventData.UserName, eventData.Reward.Title, eventData.Reward.Cost);
                var reward = await Server.Instance.chatgpt.GetImage(eventData.Reward.Title);
                if (reward != null)
                {
                    Respond($"@{eventData.UserName}: {reward}");
                    var agent = new FileGenerator.FileGenerator.Agent("user", eventData.UserName);
                    var imageFile = await Server.Instance.file.SaveImage(reward, agent);
                    Server.Instance.file.PostToWebsite(agent, new FileGenerator.FileGenerator.Post(
                        Type:"reward",
                        Title: eventData.Reward.Title,
                        Image: imageFile, 
                        Message: $"a huge waste of {eventData.Reward.Cost} sweet rolls")
                    );
                }
            }
            return;
        }

        private async Task EventSub_OnPredictionStarted(object? sender, ChannelPredictionBeginArgs e)
        {
            var prediction = e.Notification.Payload.Event;
            log.Info($"Prediction Started: {prediction.Title}");
            await Server.Instance.Assistant.AnnouncePrediction(prediction);
        }

        private async Task EventSub_OnPredictionLocked(object? sender, ChannelPredictionLockArgs e)
        {
            var prediction = e.Notification.Payload.Event;
            log.Info($"Prediction Locked: {prediction.Title}");
            await Server.Instance.Assistant.AnnouncePredictionLocked(prediction);
        }

        private async Task EventSub_OnPredictionEnded(object? sender, ChannelPredictionEndArgs e)
        {
            var prediction = e.Notification.Payload.Event;
            log.Info($"Prediction Ended: {prediction.Title}");
            log.Info($"Winning outcome: {prediction.Outcomes.Where(outcome => outcome.Id == prediction.WinningOutcomeId).First().Title}");
            await Server.Instance.Assistant.ConcludePrediction(prediction);
        }

        private async Task EventSub_OnChannelUpdate(object? sender, ChannelUpdateArgs e)
        {
            var update = ChannelInfo.FromChannelUpdate(e.Notification.Payload.Event);

            log.Info($"Old: {CurrentChannelInfo}");
            log.Info($"New: {update}");
            await Server.Instance.Assistant.RespondToChannelUpdate(CurrentChannelInfo, update);
            CurrentChannelInfo = update;
        }

        private Task EventSub_Error(object? sender, ErrorOccuredArgs e)
        {
            log.Info($"Error from EventSub: {e.Exception} - {e.Message}");
            return Task.CompletedTask;
        }
        #endregion EventSub Handlers
    }
}
