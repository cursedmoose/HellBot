using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Model
{
    public record TwitchUser(
        string UserName,
        string Channel
    )
    {
        public static TwitchUser FromChatMessage(ChatMessage message) 
        {
            return new TwitchUser(
                UserName: message.DisplayName,
                Channel: message.Channel
            );
        }
    }
}
