using TwitchLib.Client.Models;

namespace TwitchBot.Twitch.Commands
{
    internal class RollDice : CommandHandler
    {
        public RollDice() : base("!roll", PermissionGroup.User)
        {
            Aliases.Add("!r");
        }

        public override void Handle(TwitchIrcBot client, ChatMessage message)
        {
            var diceType = StripCommandFromMessage(message).Replace("d", "");
            int diceRoll;

            try
            {
                var maxValue = int.Parse(diceType);
                diceRoll = new Random().Next(1, maxValue);
            }
            catch
            {
                diceRoll = new Random().Next(1, 21);
            }


            client.RespondTo(message, $"{message.Username} rolled a {diceRoll}!");
        }
    }
}
