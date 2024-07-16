using System.Text.Json;
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

        public void Handle(EventSubWebsocketClient client, string jsonString, JsonSerializerOptions serializerOptions)
        {
            try
            {
                EventSubNotification<ChannelAdBreakBegin> eventSubNotification = JsonSerializer.Deserialize<EventSubNotification<ChannelAdBreakBegin>>(jsonString.AsSpan(), serializerOptions);
                if (eventSubNotification == null)
                {
                    throw new InvalidOperationException("Parsed JSON cannot be null!");
                }

                RaiseEvent(client, "ChannelAdBreakBegin", new ChannelAdBreakBeginArgs
                {
                    Notification = eventSubNotification
                });
            }
            catch (Exception exception)
            {
                RaiseEvent(client, "ErrorOccurred", new ErrorOccuredArgs());
            }
        }

        internal void RaiseEvent(EventSubWebsocketClient client, string eventName, object args = null)
        {
            MulticastDelegate multicastDelegate = GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) as MulticastDelegate;
            if ((object)multicastDelegate != null)
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
