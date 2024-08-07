﻿using System.Text.Json;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.Handler;
using TwitchLib.EventSub.Websockets.Core.Models;
using TwitchLib.EventSub.Websockets;
using System.Reflection;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace TwitchBot.Twitch.NotificationHandlers
{
    public class ChannelAdBreakBeginHandler : INotificationHandler
    {
        public string SubscriptionType => "channel.ad_break.begin";
        Logger log = new Logger("ChannelAdBreakBeginHandler");

        public void Handle(EventSubWebsocketClient client, string jsonString, JsonSerializerOptions serializerOptions)
        {
            log.Info("Received handle request");
            try
            {
                EventSubNotification<ChannelAdBreakBegin>? eventSubNotification = JsonSerializer.Deserialize<EventSubNotification<ChannelAdBreakBegin>>(jsonString.AsSpan(), serializerOptions) 
                    ?? throw new InvalidOperationException("Parsed JSON cannot be null!");

                RaiseEvent(client, "ChannelAdBreakBegin", new ChannelAdBreakBeginArgs
                {
                    Notification = eventSubNotification
                });
            }
            catch (Exception exception)
            {
                log.Error($"{exception.Message}");
                RaiseEvent(client, "ErrorOccurred", new ErrorOccuredArgs());
            }
        }

        internal void RaiseEvent(EventSubWebsocketClient client, string eventName, object? args = null)
        {
            MulticastDelegate? multicastDelegate = GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) as MulticastDelegate;
            if ((object?)multicastDelegate != null)
            {
                Delegate[] invocationList = multicastDelegate.GetInvocationList();
                foreach (Delegate @delegate in invocationList)
                {
                    @delegate.Method.Invoke(@delegate.Target, (args == null) ? new object[2]
                    {
                        client,
                        EventArgs.Empty
                    } : new object[2] { this, args });
                }
            }
        }
    }
}
