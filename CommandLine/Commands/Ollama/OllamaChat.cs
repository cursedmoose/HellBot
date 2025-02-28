namespace TwitchBot.CommandLine.Commands.Ollama
{
    internal class OllamaChat : ServerCommand
    {
        public OllamaChat() : base("ollama")
        {
        }

        public override async void Handle(Server server, string command)
        {
            try
            {
                var subCommand = StripCommandFromMessage(command).Split(" ", 2);

                if (subCommand.Length == 2) 
                {
                    if (subCommand[0] == "start")
                    {
                        Server.Instance.ollama.StartChat(subCommand[1], subCommand[1]);
                    }
                    else
                    {
                        var chat = subCommand[0];
                        var prompt = subCommand[1];
                        Log.Info($"Generating words for : \"{prompt}\"");
                        var response = await Server.Instance.ollama.GetResponse(chat, prompt);
                        Log.Info(response);
                    }
                }
                

                
            }
            catch (Exception e)
            {
                Log.Info($"Exceptioned out. Got {e.Message}");
            }
        }
    }
}
