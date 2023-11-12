using TwitchBot;
using TwitchBot.Twitch;
using TwitchBot.ElevenLabs;
using TwitchBot.Discord;
using TwitchBot.ChatGpt;
using System.Globalization;
using System.Text.RegularExpressions;
using TwitchBot.Assistant;
using TwitchBot.OBS;
using TwitchBot.OBS.Scene;
using TwitchBot.FileGenerator;
using TwitchBot.CommandLine;
using TwitchBot.CommandLine.Commands;
using TwitchBot.CommandLine.Commands.Assistant;
using TwitchBot.CommandLine.Commands.Discord;
using System.Reflection.Metadata;

var multiOut = new MultiWriter(Console.Out, $"logs/{DateTime.Now:yyyy-MM-dd}.txt");
Console.SetOut(multiOut);
var log = new Logger("MAIN");

log.Info("~~~~~~~~~~~~~");
log.Info("Hello, World!");

Server server = Server.Instance;
VoiceProfiles.LoadProfiles();

List<ServerCommand> Commands = new List<ServerCommand>()
{
    #region Server Commands
    new HealthCheck(),
    new ServiceUsage(),
    new StopServer(),
    #endregion
    #region Assistant Commands
    new CleanUpAssistant(),
    new StartAssistant(),
    new StopAssistant(),
    new RunAdvertisement(),
    #endregion
    #region Discord Commands
    new GetCurrentPresence(),
    #endregion
};
ServerCommand.ValidateCommandList(Commands);

while (true)
{
    var next = Console.ReadLine();
    if (next == null || next.Trim().Length == 0)
    {
        continue;
    }
    else if(next.StartsWith("commands"))
    {
        Console.WriteLine("Available Commands:");
        foreach (ServerCommand handler in Commands)
        {
            Console.WriteLine($"\t{handler.Command}");
        }
    }
    else
    {
        var handled = false;
        foreach (ServerCommand handler in Commands)
        {
            if (!handled && handler.CanHandle(next))
            {
                log.Info($"Command {next} is being handled by {handler.GetType()}");
                handler.Handle(server, next);
                handled = true;
                break;
            }
        }

        if (!handled)
        {
            log.Info($"No handler found for {next}. Falling back to if statements.");
        }
    }

    if (next.Contains("dalle"))
    {
        try
        {
            var prompt = next[5..].Trim();
            log.Info($"Generating image for : \"{prompt}\"");
            var image = await server.chatgpt.GetImage(prompt);
            var imageFile = await server.file.SaveImage(image, server.Assistant.Agent);
            log.Info($"{image}");
        }
        catch (Exception e)
        {
            log.Info($"Exceptioned out. Got {e.Message}");
        }
    }
    else if (next.Contains("gpt"))
    {
        try
        {
            var prompt = next[3..].Trim();
            log.Info($"Generating words for : \"{prompt}\"");
            log.Info(await server.chatgpt.GetResponseText(server.Assistant.Persona, prompt));
        }
        catch (Exception e)
        {
            log.Info($"Exceptioned out. Got {e.Message}");
        }
    }
    else if (next.Contains("chatters"))
    {
        var chatters = await Server.Instance.twitch.GetChatterNames();
        chatters.ForEach(log.Info);
    }
    else if (next.Contains("create"))
    {
        var guid = Guid.NewGuid().ToString();
        server.file.PostToWebsite(
            new FileGenerator.Agent("assistant", server.Assistant.Name),
            new FileGenerator.Post("reward", "test!", guid, "This was a test!")
            );
    }
    else if (next.Contains("delete"))
    {

    }
    else if (next.Contains("paint"))
    {
        (server.Assistant as Sheogorath)?.PaintPicture();
    }
    else if (next.Contains("obs"))
    {
        server.obs.GetActiveSource();
    }
    else if (next.Contains("id"))
    {
        var game = next[2..].Trim();
        if (game.Length > 1)
        {
            await server.twitch.ChangeGame(game);
        }
    }
    else if (next.Contains("on"))
    {
        var numOption = next.Split(" ");
        if (numOption.Length > 1)
        {
            var num = numOption[1];
            server.obs.EnableScene("Characters", int.Parse(num));
        }
        else
        {
            server.obs.EnableScene(ObsScenes.Sheogorath);
        }
    }
    else if (next.Contains("off"))
    {
        var numOption = next.Split(" ");
        if (numOption.Length > 1)
        {
            var num = numOption[1];
            server.obs.DisableScene("Characters", int.Parse(num));
        }
        else
        {
            server.obs.DisableScene(ObsScenes.Sheogorath);
        }
    }
    else if (next.Contains("test"))
    {

    }
    else
    {

    }
}

public class Server
{
    public static readonly CultureInfo LOG_FORMAT = new("en-GB");
    public static readonly Regex WEBSITE_REGEX = new("[(http(s)?):\\/\\/(www\\.)?a-zA-Z0-9@:%._\\+~#=]{2,256}\\.[a-z]{2,6}\\b([-a-zA-Z0-9@:%_\\+.~#?&//=]*)", RegexOptions.IgnoreCase);
    public static readonly Regex EMOTE_REGEX = new("cursed99");

    static readonly bool GLOBAL_ENABLE = false;

    public Assistant Assistant = new Sheogorath();

    public TwitchIrcBot twitch = new(GLOBAL_ENABLE);
    public ElevenLabs elevenlabs = new(GLOBAL_ENABLE);
    public DiscordBot discord = new(GLOBAL_ENABLE);
    public ChatGpt chatgpt = new(GLOBAL_ENABLE);
    public ObsClient obs = new(GLOBAL_ENABLE);
    public HttpClient web = new();
    public FileGenerator file = new();

    private static readonly Lazy<Server> lazy = new(() => new Server());
    public static Server Instance { get { return lazy.Value; } }

    public async void SaveImageAs(string webUrl, string filename, string fileExtension = "")
    {
        var response = await web.GetAsync(webUrl);
        var sourceFile = Path.Combine("images", RemoveInvalidChars(filename) + fileExtension);
        var latest = Path.Combine("images", $".latest{fileExtension}");
        using (var fs = new FileStream(sourceFile, FileMode.Create))
        {
            await response.Content.CopyToAsync(fs);
        }
        File.Copy(sourceFile, latest, true);
    }

    public async Task<string> ShortenUrl(string longUrl)
    {
        var response = await web.GetAsync($"https://tinyurl.com/api-create.php?url={longUrl}");
        return await response.Content.ReadAsStringAsync();
    }

    private static string RemoveInvalidChars(string filename)
    {
        return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
    }

    public Bitmap CaptureScreen()
    {
        Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
        }
        return bmp;
    }

    public string TakeScreenshot()
    {
        var filePath = "images/screenshots/latest.png";
        var img = CaptureScreen();
        using (var fs = new FileStream(filePath, FileMode.Create))
        {
            img.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
        }
        return filePath;
    }

    public async Task<string> TakeAndUploadScreenshot()
    {
        var img = TakeScreenshot();
        var fileUrl = await discord.UploadFile(img);
        return fileUrl;
    }

    private Server()
    {

    }
}
