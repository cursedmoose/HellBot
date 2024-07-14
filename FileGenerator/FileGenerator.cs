using System.Text.Json;
using TwitchBot.AWS;
using TwitchBot.Config;

namespace TwitchBot.FileGenerator
{
    public class FileGenerator
    {
        private const string WSL_PATH = @"\\wsl.localhost\Ubuntu\home\cursedmoose\app\website\cursedmoose.github.io\";
        private const string ASSETS = "assets";
        private const string IMAGES = "images";
        private const string CONFIG = "config";
        public FileGenerator()
        {
            
        }

        protected readonly Logger log = new("File");
        readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public record Agent(string Type, string Name);
        public record UserAgent(string Name) : Agent("user", Name);
        public record AssistantAgent(string Name) : Agent("assistant", Name);
        public record Post(string Type, string Title, string Image, string Message = "");

        public async Task<string> SaveImage(string webUrl, Agent agent)
        {
            log.Info($"Saving image from {webUrl}");
            var imageReference = Guid.NewGuid().ToString();
            var imagePath = $"{IMAGES}/{agent.Type}/{agent.Name.ToLower()}/";
            Directory.CreateDirectory(imagePath);
            log.Info($"Creating directory at {imagePath}");


            var response = await Server.Instance.web.GetAsync(webUrl);
            var sourceFile = Path.Combine(imagePath, imageReference + ".png");
            var latest = Path.Combine(IMAGES, $".latest.png");
            using (var fs = new FileStream(sourceFile, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }
            File.Copy(sourceFile, latest, true);
            CopyImageToJekyll(agent, sourceFile);
            CopyToS3(sourceFile);

            return imageReference;
        }


        private void CopyPostToJekyll(string origin)
        {
            var copyTo = Path.Combine(@"\\wsl.localhost\Ubuntu\home\cursedmoose\app\website\cursedmoose.github.io\", origin);
            log.Info($"Copying file {origin}");

            File.Copy(origin, copyTo, true);
        }

        private async void CopyToS3(string origin)
        {
            await Server.Instance.aws.UploadToS3(origin, origin);
        }

        private void CopyImageToJekyll(Agent agent, string origin)
        {
            var directoryPath = Path.Combine(WSL_PATH, ASSETS, IMAGES, agent.Type, agent.Name.ToLower());
            Directory.CreateDirectory(directoryPath);
            log.Info($"Creating directory at {directoryPath}");
            var copyTo = Path.Combine(@"\\wsl.localhost\Ubuntu\home\cursedmoose\app\website\cursedmoose.github.io\assets", origin);
            log.Info($"Copying file {origin}");
            File.Copy(origin, copyTo, true);
        }

        public void PostToWebsite(Agent agent, Post post)
        {
            var date = DateTime.Now;
            var fileDate = date.ToString("yyyy-MM-dd");
            var sourceFile = Path.Combine("_posts", $"{fileDate}-{post.Type}-{post.Image}.markdown");
            var imageTarget = AwsConfig.CloudfrontDomain + $"/{IMAGES}/{agent.Type}/{agent.Name.ToLower()}/{post.Image}.png";
            Directory.CreateDirectory("_posts");
            File.WriteAllText(
                path: sourceFile,
                contents: 
                "---\r\n" +
                $"layout: {post.Type}\r\n" +
                $"category: {post.Type}\r\n" +
                $"date:   {date:yyyy-MM-dd HH:mm:ss -0800}\r\n" +
                $"author: {agent.Name.ToLower()}\r\n" +
                $"title: {agent.Name}'s {post.Title}\r\n" +
                $"reward: {post.Title}\r\n" +
                $"image: {imageTarget}\r\n" +
                "---\r\n" +
                $"{post.Message}"
            );

            CopyPostToJekyll(sourceFile);
        }

        public static string RemoveInvalidChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        public string CreateAgentConfig(Agent agent, string configType, object configData)
        {
            var agentDirectory = Path.Combine(CONFIG, agent.Type, agent.Name);
            var fullPath = Path.Combine(agentDirectory, configType.ToLower() + ".json");
            log.Info($"Creating {fullPath}");
            Directory.CreateDirectory(agentDirectory);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(configData, jsonOptions));
            CopyToS3(fullPath);
            return fullPath;
        }
        
        public async Task<T?> LoadAgentConfig<T>(Agent agent, string configType)
        {
            var agentDirectory = string.Join("/", CONFIG, agent.Type, agent.Name); // Path.Combine(CONFIG, agent.Type, agent.Name);
            var fullPath = string.Join("/", agentDirectory, configType.ToLower() + ".json"); // Path.Combine(agentDirectory, configType.ToLower() + ".json");

            try
            {
                var jsonString = await Server.Instance.aws.ReadFromS3(fullPath);

                if (jsonString != null)
                {
                    return JsonSerializer.Deserialize<T>(jsonString, jsonOptions);
                }
                else
                {
                    jsonString = File.ReadAllText(fullPath);
                    return JsonSerializer.Deserialize<T>(jsonString, jsonOptions);
                }
            }
            catch (Exception ex) 
            {
                log.Error($"Could not load config {fullPath} due to {ex.Message}");
                return default;
            }
        }
    }
}
