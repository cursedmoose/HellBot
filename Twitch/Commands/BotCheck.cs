using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class BotCheck : CommandHandler
    {        
        public BotCheck() : base(command: "!botcheck", users: PermissionGroup.User)
        {
            Aliases.Add("!hello");
        }
        public override void Handle(TwitchIrcBot client, ChatMessage message)
        {
            client.RespondTo(message, "TTS Bot is working.");
        }
    }
}
