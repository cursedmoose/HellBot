using TwitchBot.Twitch;
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
using TwitchBot.Config;
using static TwitchBot.Config.TwitchConfig;
using System.Runtime.Caching;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using System.Runtime.InteropServices;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

public class TwitchIrcBot
{
    bool Enabled = false;
    TwitchClient client;
    TwitchAPI api;
    EventSubWebsocketClient events;
    WebsocketClient EventSubSocket;
    TwitchTts tts;

    Helix API { get { return api.Helix; } }
    Auth Auth { get { return api.Auth; } }

    List<CommandHandler> commands;
    MemoryCache eventLog = new MemoryCache("Events");

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
        Console.WriteLine($"{timestamp} [Twitch] {message}");
    }
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

        Init_API();
        Init_EventSub();
        Init_IRC_Client();

        var scopes = new List<string>()
        {
            "moderator:manage:banned_users",
            "moderator:read:chatters",
            "moderator:read:followers",
            "channel:manage:polls",
            "channel:manage:redemptions",
            "channel:manage:broadcast",
            "channel:manage:redemptions"
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
        Log($"[Client] Initialized: {client.IsInitialized} | Connected: {client.IsConnected}");
        Log($"[API] Valid: {Auth.ValidateAccessTokenAsync().Result.Login}");
        Log($"[Events] Connected: {EventSubSocket.IsConnected}");
        var subs = await API.EventSub.GetEventSubSubscriptionsAsync(status: "enabled");
        Log($"[Events] Subscriptions: {subs.Subscriptions.Length} | Cost: {subs.TotalCost}");

        foreach (var sub in subs.Subscriptions)
        {
            foreach (var condition in sub.Condition)
            {
                Log($"[Events]\t[{sub.Type}]: {condition.Key}={condition.Value}");
            }
            Log($"[Events]\t[{sub.Type}]: {sub.Status} ({sub.Cost})");
            Log($"[Events]\t[{sub.Type}]: {sub.CreatedAt}");
        }
    }

    private void Init_API()
    {
        api = new();
        api.Settings.ClientId = AccountInfo.API_CLIENT_ID;
        api.Settings.Scopes = new List<AuthScopes>() {
            AuthScopes.Helix_Moderator_Read_Chatters,
            AuthScopes.Helix_Moderator_Read_Followers,
            AuthScopes.Helix_Channel_Manage_Polls
        };
    }

    private void Init_IRC_Client()
    {
        ConnectionCredentials credentials = new ConnectionCredentials(
            twitchUsername: AccountInfo.ACCOUNT_NAME, 
            twitchOAuth: AccountInfo.OAUTH_PASSWORD);
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };

        WebSocketClient customClient = new WebSocketClient(clientOptions);
        client = new TwitchClient(customClient);
        client.Initialize(credentials, AccountInfo.CHANNEL);

        client.OnLog += Client_OnLog;
        client.OnJoinedChannel += Client_OnJoinedChannel;
        client.OnMessageReceived += Client_OnMessageReceived;
        client.OnWhisperReceived += Client_OnWhisperReceived;
        client.OnNewSubscriber += Client_OnNewSubscriber;
        client.OnReSubscriber += Client_OnReSubscriber;
        client.OnConnected += Client_OnConnected;
    }

    private void Init_EventSub()
    {
        WebsocketClient customClient = new WebsocketClient();
        EventSubSocket = customClient;
        ServiceContainer sp = new ServiceContainer();
        sp.AddService(customClient.GetType(), EventSubSocket);
        ILogger<EventSubWebsocketClient> logger = new NullLogger<EventSubWebsocketClient>();
        List<INotificationHandler> handlers = new List<INotificationHandler>()
        {
            new ChannelPointsCustomRewardRedemptionAddHandler(),
            new ChannelPollBeginHandler(),
            new ChannelPollEndHandler(),
        };

        events = new(logger, handlers, sp, customClient);
        events.WebsocketConnected += EventSub_OnConnected;
        events.WebsocketDisconnected += EventSub_OnDisconnected;
        events.WebsocketReconnected += EventSub_OnReconnect;
        events.ErrorOccurred += EventSub_Error;

        events.ChannelPollBegin += EventSub_OnPollBegin;
        events.ChannelPollEnd += EventSub_OnPollEnd;
        events.ChannelPointsCustomRewardRedemptionAdd += EventSub_OnChannelPointsRedeemed;
        events.ChannelFollow += EventSub_OnChannelFollow;

        Log("Event sub initialized.");
    }

    private async Task StartApi(List<string> scopes)
    {
        var server = new AuthServer(AccountInfo.API_REDIRECT_URL);
        var codeUrl = AuthServer.getAuthorizationCodeUrl(AccountInfo.API_CLIENT_ID, AccountInfo.API_REDIRECT_URL, scopes);
        Console.WriteLine($"Please authorize here:\n{codeUrl}");
        System.Diagnostics.Process.Start(@"C:\Program Files\Mozilla Firefox\firefox.exe", codeUrl);
        var auth = await server.Listen();
        var resp = await Auth.GetAccessTokenFromCodeAsync(auth.Code, AccountInfo.API_CLIENT_SECRET, AccountInfo.API_REDIRECT_URL);
        api.Settings.AccessToken = resp.AccessToken;
        var user = (await API.Users.GetUsersAsync()).Users[0];
        // Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {resp.AccessToken}\nRefresh token: {resp.RefreshToken}\nExpires in: {resp.ExpiresIn}\nScopes: {string.Join(", ", resp.Scopes)}");
        Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName}\nScopes: {string.Join(", ", resp.Scopes)}");

    }

    public void RespondTo(ChatMessage message, string withMessage)
    {
        Log($"Sending {withMessage} to {message.Channel}");
        client.SendMessage(message.Channel, withMessage);
    }

    public void Respond(string withMessage)
    {
        if (Enabled)
        {
            Log($"Sending {withMessage} to {AccountInfo.CHANNEL}");
            client.SendMessage(AccountInfo.CHANNEL, withMessage);
        }
    }
    public void stop()
    {
        client.Disconnect();
        events.DisconnectAsync();
        Log("TwitchBot Disconnected.");
    }

    #region TwitchClient Handlers
    private void Client_OnLog(object? sender, OnLogArgs e)
    {
        Log($"{e.BotUsername} - {e.Data}");
    }

    private void Client_OnConnected(object? sender, OnConnectedArgs e)
    {
        Log($"Connected to {e.AutoJoinChannel}");
    }

    private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        Log("Bot has joined channel.");
        // client.SendMessage(e.Channel, "beep boop. bot online.");
    }

    private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (e.ChatMessage.Message.StartsWith('!'))
        {
            Log($"Message looks like a bot command.");
            handleCommand(sender, e);
            return;
        }

        Log($"{e.ChatMessage.Username} sent message: ${e.ChatMessage.Message}");

        if (Admins.isAdmin(e.ChatMessage.Username))
        {
            playTts(sender, e);
        }
        else
        {
            playRumorTts(sender, e);
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
        //client.TimeoutUser(e.Channel, e.Subscriber.DisplayName, TimeSpan.FromMinutes(5), "sub??? in my channel???");
        // var prompt = $"welcome new subscriber \"{e.Subscriber.DisplayName}\"";
        //Server.Instance.chatgpt.getResponse("Shegorath", prompt);
        Server.Instance.Assistant.WelcomeSubscriber(e.Subscriber.DisplayName, 1);
    }

    private void Client_OnReSubscriber(object? sender, OnReSubscriberArgs e)
    {
        Server.Instance.Assistant.WelcomeSubscriber(e.ReSubscriber.DisplayName, e.ReSubscriber.Months);
    }

    #endregion TwitchClient Handlers

    private void handleCommand(object? sender, OnMessageReceivedArgs e)
    {
        foreach (CommandHandler handler in commands)
        {
            if (handler.canHandle(client, e.ChatMessage))
            {
                handler.handle(client, e.ChatMessage);
                return;
            }
        }

        Log($"No handler found for {e.ChatMessage.Message}.");
        return;
    }

    #region Tts
    private void playTts(object? sender, OnMessageReceivedArgs e)
    {
        if (Enabled)
        {
            tts.play(e.ChatMessage);
        }
    }

    private void playRumorTts(object? sender, OnMessageReceivedArgs e)
    {
        if (Enabled)
        {
            tts.playRumor(e.ChatMessage);
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
        if (!client.IsConnected || string.IsNullOrWhiteSpace(title) || choices.Count <= 0)
        {
            Log($"Client: {client.IsConnected} | Title: {title} | Choices: {choices.Count}");
            return false;
        }

        var request = new CreatePollRequest();
        request.DurationSeconds = 60;
        request.BroadcasterId = TwitchConfig.AccountInfo.USER_ID;
        request.Title = title;

        List<Choice> realChoices = new();
        foreach (string choice in choices)
        {
            var choiceObj = new Choice();
            choiceObj.Title = choice;
            realChoices.Add(choiceObj);
        }

        request.Choices = realChoices.ToArray();

        Log("Creating Poll...");
        await API.Polls.CreatePollAsync(request);

        return true;
    }

    public async void ChangeTitle(string newTitle)
    {
        ModifyChannelInformationRequest request = new();
        request.Title = newTitle + " | !hellbot";
        await API.Channels.ModifyChannelInformationAsync(broadcasterId: AccountInfo.USER_ID, request: request);
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
        var request = new BanUserRequest();
        request.UserId = username;
        request.Duration = timeoutSeconds;
        request.Reason = reason;

        var ban = await API.Moderation.BanUserAsync(
            broadcasterId: AccountInfo.USER_ID,
            moderatorId: AccountInfo.USER_ID,
            banUserRequest: request
        );

        return true;
    }

    public async Task<string> CreateCustomReward(string rewardName, int rewardCost, string prompt = null)
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
        catch ( Exception ex )
        {
            Log($"Couldn't delete reward {rewardId} due to {ex.Message}");
            return false;
        }

        return true;


    }

    #endregion API Hooks

    #region EventSub Handlers
    private async void EventSub_OnConnected(object? sender, WebsocketConnectedArgs e)
    {
        Log("EventSub Connected. ~~~~~~~~~~~~~~~");
        if (!e.IsRequestedReconnect)
        {
            var accessToken = await Auth.GetAccessTokenAsync();
            var valid = await Auth.ValidateAccessTokenAsync(accessToken);
            Log($"Token is valid for {valid.UserId}");
            Log($"SessionId is {events.SessionId}");

            var response = await API.EventSub.CreateEventSubSubscriptionAsync(
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

            var response2 = await API.EventSub.CreateEventSubSubscriptionAsync(
                type: "channel.poll.begin",
                version: "1",
                condition: new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", AccountInfo.USER_ID },
                },
                method: EventSubTransportMethod.Websocket,
                websocketSessionId: events.SessionId
            );

            var response3 = await API.EventSub.CreateEventSubSubscriptionAsync(
                type: "channel.poll.end",
                version: "1",
                condition: new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", AccountInfo.USER_ID },
                },
                method: EventSubTransportMethod.Websocket,
                websocketSessionId: events.SessionId
            );

            var response4 = await API.EventSub.CreateEventSubSubscriptionAsync(
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
        Log($"EventSub Disconnected. ~~~~~~~~~~~~~~~ Retry?");
        while (!await events.ReconnectAsync())
        {
            Log("Websocket reconnect failed!");
            await Task.Delay(1000);
        }
    }

    private void EventSub_OnReconnect(object? sender, EventArgs e)
    {
        Log($"Websocket {events.SessionId} reconnected");
    }
    private void EventSub_OnChannelFollow(object? sender, ChannelFollowArgs e)
    {
        var eventData = e.Notification.Payload.Event;
        Log($"{eventData.UserName} followed {eventData.BroadcasterUserName} at {eventData.FollowedAt}");
        Server.Instance.Assistant.WelcomeFollower(eventData.UserName);
    }


    private void EventSub_OnPollBegin(object? sender, ChannelPollBeginArgs e)
    {
        var eventData = e.Notification.Payload.Event;
        Log($"Poll started: {eventData.Title}");
        Log($"{eventData.Choices[0].Title}");
        Server.Instance.Assistant.AnnouncePoll(eventData.Title, eventData.Choices.Select(choice => choice.Title).ToList());
    }

    private void EventSub_OnPollEnd(object? sender, ChannelPollEndArgs e)
    {
        var eventData = e.Notification.Payload.Event;

        if (eventLog[eventData.Id] != null ) { return; }
        eventLog.Add(eventData.Id, "end", DateTime.Now.AddMinutes(5));

        Log($"Poll ended: {eventData.Title}");
        Log("Results:");
        foreach (PollChoice choice in eventData.Choices.ToList())
        {
            Log($"{choice.Title}: {choice.Votes}");
        }
        Server.Instance.Assistant.ConcludePoll(eventData.Title, eventData.Choices.MaxBy(it => it.Votes).Title);
    }

    private async void EventSub_OnChannelPointsRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        var eventData = e.Notification.Payload.Event;
        Log($"{eventData.UserName} redeemed {eventData.Reward.Title}");
        await Server.Instance.Assistant.ChannelRewardClaimed(eventData.UserName, eventData.Reward.Title, eventData.Reward.Cost);
        var reward = await Server.Instance.chatgpt.getImage(eventData.Reward.Title);
        if (reward != null)
        {
            Respond($"@{eventData.UserName}: {reward}");
        }
    }

    private void EventSub_Error(object? sender, ErrorOccuredArgs e)
    {
        Log($"Error from EventSub: {e.Exception} - {e.Message}");
    }
    #endregion EventSub Handlers
}
