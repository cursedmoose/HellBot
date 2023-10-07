using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class BotCheck : CommandHandler
    {        
        public bool canHandle(TwitchClient client, ChatMessage message)
        {
            return message.Message.ToLower().StartsWith("!botcheck");
        }
        public void handle(TwitchClient client, ChatMessage message)
        {
            client.SendMessage(message.Channel, "TTS Bot is working.");
        }
    }
}
