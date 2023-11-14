namespace TwitchBot.CommandLine.Commands.OpenAI
{
    internal class ChatGptGeneration : ServerCommand
    {
        public ChatGptGeneration() : base("gpt")
        {
        }

        public override async void Handle(Server server, string command)
        {
            try
            {
                var prompt = StripCommandFromMessage(command);
                Log.Info($"Generating words for : \"{prompt}\"");
                Log.Info(await server.chatgpt.GetResponseText(server.Assistant.Persona, prompt));
            }
            catch (Exception e)
            {
                Log.Info($"Exceptioned out. Got {e.Message}");
            }
        }
    }
}
