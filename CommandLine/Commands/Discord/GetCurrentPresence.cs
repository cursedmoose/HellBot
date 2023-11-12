using Discord;
using TwitchBot.Discord;

namespace TwitchBot.CommandLine.Commands.Discord
{
    internal class GetCurrentPresence : ServerCommand
    {
        public GetCurrentPresence() : base("presence") { }

        public override async void Handle(Server server, string command)
        {
            var activity = await server.discord.GetPresence();
            Console.WriteLine($"Last Known Game:  {DiscordBot.LastKnownGame}");
            Console.WriteLine($"Last Known State: {DiscordBot.LastKnownState}");

            Console.WriteLine($"Current Activity: {activity.GetType()}");
            if (activity is RichGame game)
            {
                Console.WriteLine("Activity is ");
                Console.WriteLine($"Type: {game.Type} | Name: {game.Name}");
                Console.WriteLine($"{game.State}");
                Console.WriteLine($"Since {game?.Timestamps?.Start?.LocalDateTime.ToString()}");
                Console.WriteLine($"{game?.Details}");
            }
            else
            {
                Console.WriteLine($"{activity.Name}");
                Console.WriteLine($"{activity.Details}");
                Console.WriteLine($"{activity.Type}");
            }
        }
    }
}
