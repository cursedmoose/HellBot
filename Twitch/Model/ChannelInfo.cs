
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace TwitchBot.Twitch.Model
{
    public record ChannelInfo(
        string BroadcasterId,
        string BroadcasterName,
        string GameName,
        string GameId,
        string Title
    )
    {
        public static ChannelInfo FromChannelInformation(ChannelInformation info)
        {
            return new ChannelInfo(
                BroadcasterId: info.BroadcasterId,
                BroadcasterName: info.BroadcasterName,
                GameName: info.GameName,
                GameId: info.GameId,
                Title: info.Title.Split('|')[0].Trim()
            );
        }

        public static ChannelInfo FromChannelUpdate(ChannelUpdate update)
        {
            return new ChannelInfo(
                BroadcasterId: update.BroadcasterUserId,
                BroadcasterName: update.BroadcasterUserName,
                GameName: update.CategoryName,
                GameId: update.CategoryId,
                Title: update.Title.Split('|')[0].Trim()
            );
        }
    }
}
