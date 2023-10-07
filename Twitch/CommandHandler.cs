using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch
{
    internal interface CommandHandler
    {
        public bool canHandle(TwitchClient client, ChatMessage message);
        public void handle(TwitchClient client, ChatMessage message);
    }
}
