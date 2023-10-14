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

namespace TwitchBot.Twitch
{
    public class TwitchIrcBot
    {
        readonly bool Enabled = false;
        readonly TwitchClient client;
        readonly TwitchAPI api;
        readonly EventSubWebsocketClient events;
        readonly WebsocketClient EventSubSocket = new();
        readonly TwitchTts tts;

        Helix API { get { return api.Helix; } }
        Auth Auth { get { return api.Auth; } }

        readonly List<CommandHandler> commands;
        readonly MemoryCache eventLog = new("Events");
        readonly Logger log = new("Twitch");
        public TwitchIrcBot(bool enabled = true)
        {
            Enabled = enabled;
            commands = new List<CommandHandler>()
        {
            new BotCheck(),
            new ElevenLabsUsage(),
            new CommemerateEvent(),
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
            "channel:manage:redemptions",
            "channel:manage:broadcast",
            "channel:manage:redemptions",
            "channel:edit:commercial"
        };

            if (enabled)
            {
                Task.Delay(1000);
                client?.Connect();
                StartApi(scopes).GetAwaiter().GetResult();
                events?.ConnectAsync();
            }
        }

        public async void HealthCheck()
        {
            log.Info($"[Client] Initialized: {client.IsInitialized} | Connected: {client.IsConnected}");
            log.Info($"[API] Valid: {Auth.ValidateAccessTokenAsync().Result.Login}");
            log.Info($"[Events] Connected: {EventSubSocket.IsConnected}");
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
            api.Settings.ClientId = AccountInfo.API_CLIENT_ID;
            api.Settings.Scopes = new List<AuthScopes>() {
            AuthScopes.Helix_Moderator_Read_Chatters,
            AuthScopes.Helix_Moderator_Read_Followers,
            AuthScopes.Helix_Channel_Manage_Polls
        };
            return api;
        }

        private TwitchClient Init_IRC_Client()
        {
            ConnectionCredentials credentials = new(
                twitchUsername: AccountInfo.ACCOUNT_NAME,
                twitchOAuth: AccountInfo.OAUTH_PASSWORD);
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
            new ChannelPointsCustomRewardRedemptionAddHandler(),
            new ChannelPollBeginHandler(),
            new ChannelPollEndHandler(),
        };

            EventSubWebsocketClient events = new(logger, handlers, sp, EventSubSocket);
            events.WebsocketConnected += EventSub_OnConnected;
            events.WebsocketDisconnected += EventSub_OnDisconnected;
            events.WebsocketReconnected += EventSub_OnReconnect;
            events.ErrorOccurred += EventSub_Error;

            events.ChannelPollBegin += EventSub_OnPollBegin;
            events.ChannelPollEnd += EventSub_OnPollEnd;
            events.ChannelPointsCustomRewardRedemptionAdd += EventSub_OnChannelPointsRedeemed;
            events.ChannelFollow += EventSub_OnChannelFollow;

            log.Info("Event sub initialized.");
            return events;
        }

        private async Task StartApi(List<string> scopes)
        {
            var server = new AuthServer(AccountInfo.API_REDIRECT_URL);
            var codeUrl = AuthServer.GetAuthorizationCodeUrl(AccountInfo.API_CLIENT_ID, AccountInfo.API_REDIRECT_URL, scopes);
            Console.WriteLine($"Please authorize here:\n{codeUrl}");
            System.Diagnostics.Process.Start(@"C:\Program Files\Mozilla Firefox\firefox.exe", codeUrl);
            var auth = await server.Listen();
            var resp = await Auth.GetAccessTokenFromCodeAsync(auth.Code, AccountInfo.API_CLIENT_SECRET, AccountInfo.API_REDIRECT_URL);
            api.Settings.AccessToken = resp.AccessToken;
            var user = (await API.Users.GetUsersAsync()).Users[0];
            Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName}\nScopes: {string.Join(", ", resp.Scopes)}");

        }

        public void RespondTo(ChatMessage message, string withMessage)
        {
            log.Info($"Sending {withMessage} to {message.Channel}");
            client.SendMessage(message.Channel, withMessage);
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
            log.Info($"{e.BotUsername} - {e.Data}");
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
                log.Info($"Message looks like a bot command.");
                HandleCommand(sender, e);
                return;
            }

            log.Info($"{e.ChatMessage.Username} sent message: ${e.ChatMessage.Message}");

            if (Admins.isAdmin(e.ChatMessage.Username))
            {
                PlayTts(sender, e);
            }
            else
            {
                PlayRumorTts(sender, e);
            }

            if (!Admins.isAdmin(e.ChatMessage.Username)) // lol admins can't get banned 
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
                if (handler.canHandle(client, e.ChatMessage))
                {
                    handler.handle(client, e.ChatMessage);
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
                tts.PlayRumor(e.ChatMessage);
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
            await API.Polls.CreatePollAsync(request);

            return true;
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
            var info = await API.Channels.GetChannelInformationAsync(AccountInfo.CHANNEL_ID);
            return info.Data[0];
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

        #endregion API Hooks

        #region EventSub Handlers
        private async void EventSub_OnConnected(object? sender, WebsocketConnectedArgs e)
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
            }
        }

        private async void EventSub_OnDisconnected(object? sender, EventArgs e)
        {
            log.Info($"EventSub Disconnected. ~~~~~~~~~~~~~~~ Retry?");
            while (!await events.ReconnectAsync())
            {
                log.Error("Websocket reconnect failed!");
                await Task.Delay(1000);
            }
        }

        private void EventSub_OnReconnect(object? sender, EventArgs e)
        {
            log.Info($"Websocket {events.SessionId} reconnected");
        }
        private void EventSub_OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
            var eventData = e.Notification.Payload.Event;
            log.Info($"{eventData.UserName} followed {eventData.BroadcasterUserName} at {eventData.FollowedAt}");
            Server.Instance.Assistant.WelcomeFollower(eventData.UserName);
        }


        private void EventSub_OnPollBegin(object? sender, ChannelPollBeginArgs e)
        {
            var eventData = e.Notification.Payload.Event;
            log.Info($"Poll started: {eventData.Title}");
            log.Info($"{eventData.Choices[0].Title}");
            Server.Instance.Assistant.AnnouncePoll(eventData.Title, eventData.Choices.Select(choice => choice.Title).ToList());
        }

        private void EventSub_OnPollEnd(object? sender, ChannelPollEndArgs e)
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
            Server.Instance.Assistant.ConcludePoll(eventData.Title, eventData.Choices.MaxBy(it => it.Votes).Title);
        }

        private async void EventSub_OnChannelPointsRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            var eventData = e.Notification.Payload.Event;
            log.Info($"{eventData.UserName} redeemed {eventData.Reward.Title}");
            await Server.Instance.Assistant.ChannelRewardClaimed(eventData.UserName, eventData.Reward.Title, eventData.Reward.Cost);
            var reward = await Server.Instance.chatgpt.GetImage(eventData.Reward.Title);
            if (reward != null)
            {
                Respond($"@{eventData.UserName}: {reward}");
            }
        }

        private void EventSub_Error(object? sender, ErrorOccuredArgs e)
        {
            log.Info($"Error from EventSub: {e.Exception} - {e.Message}");
        }
        #endregion EventSub Handlers
    }
}
