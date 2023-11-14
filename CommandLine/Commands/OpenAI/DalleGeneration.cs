namespace TwitchBot.CommandLine.Commands.OpenAI
{
    internal class DalleGeneration : ServerCommand
    {
        public DalleGeneration() : base("dalle")
        {
        }

        public override async void Handle(Server server, string command)
        {
            try { 
                var prompt = StripCommandFromMessage(command);
                Log.Info($"Generating image for : \"{prompt}\"");
                var image = await server.chatgpt.GetImage(prompt);
                var imageFile = await server.file.SaveImage(image, server.Assistant.Agent);
                Log.Info($"{image}");
                Log.Info($"Saved to {imageFile}");
            }
            catch (Exception e)
            {
                Log.Info($"Exceptioned out. Got {e.Message}");
            }
        }
    }
}
