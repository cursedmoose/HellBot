using TwitchBot;
using TwitchBot.Twitch;
using TwitchBot.ElevenLabs;
using TwitchBot.Discord;
using TwitchBot.ChatGpt;
using System.Globalization;
using System.Text.RegularExpressions;
using TwitchBot.Assistant;
using TwitchBot.OBS;
using TwitchBot.FileGenerator;
using TwitchBot.CommandLine;
using TwitchBot.CommandLine.Commands;
using TwitchBot.CommandLine.Commands.Assistant;
using TwitchBot.CommandLine.Commands.Discord;
using TwitchBot.CommandLine.Commands.OpenAI;
using TwitchBot.CommandLine.Commands.OBS;
using TwitchBot.CommandLine.Commands.ServerOptions;
using TwitchBot.SpeechToText;
using TwitchBot.ScreenCapture;
using TwitchBot.Hotkeys;
using TwitchBot.OCR;

var multiOut = new MultiWriter(Console.Out, $"logs/{DateTime.Now:yyyy-MM-dd}.txt");
Console.SetOut(multiOut);
var log = new Logger("MAIN");

log.Info("~~~~~~~~~~~~~");
log.Info("Hello, World!");

Server server = Server.Instance;
VoiceProfiles.LoadProfiles();

List<ServerCommand> Commands = new()
{
    #region Server Commands
    new HealthCheck(),
    new ServiceUsage(),
    new StartSubroutine(),
    new StopSubroutine(),
    new TestCommand(),
    #endregion
    #region Assistant Commands
    new CleanUpAssistant(),
    new RunAdvertisement(),
    new PlayTts(),
    new Narrate(),
    #endregion
    #region Discord Commands
    new GetCurrentPresence(),
    #endregion
    #region OpenAI Commands
    new DalleGeneration(),
    new ChatGptGeneration(),
    #endregion
    #region OBS Commands
    new PrintSources()
    #endregion
};
ServerCommand.ValidateCommandList(Commands);
HotKeyManager.Initialize();

while (true)
{
    var next = Console.ReadLine();
    if (next == null || next.Trim().Length == 0)
    {
        continue;
    }
    else if (next.StartsWith("commands"))
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
            log.Info($"No handler found for {next}.");
        }
    }
}

public class ServerConfig
{
    public static readonly bool ENABLED = true;
    public static readonly bool DISABLED = false;

    public static readonly bool Twitch = DISABLED;
    public static readonly bool ElevenLabs = ENABLED;
    public static readonly bool Discord = ENABLED;
    public static readonly bool ChatGpt = ENABLED;
    public static readonly bool Obs = ENABLED;
    public static readonly bool Speech = DISABLED;
}

public class Server
{
    public static readonly CultureInfo LOG_FORMAT = new("en-GB");
    public static readonly Regex WEBSITE_REGEX = new("[(http(s)?):\\/\\/(www\\.)?a-zA-Z0-9@:%._\\+~#=]{2,256}\\.[a-z]{2,6}\\b([-a-zA-Z0-9@:%_\\+.~#?&//=]*)", RegexOptions.IgnoreCase);
    public static readonly Regex EMOTE_REGEX = new("cursed99");

    private Logger log = new("Server");

    static Assistant Sheogorath = new Sheogorath();
    static Assistant Werner = new Werner();

    public Assistant Assistant = Sheogorath;
    public Assistant Narrator = Werner;

    private List<Assistant> Assistants = new()
    {
        Sheogorath,
        Werner
    };

    public TwitchIrcBot twitch = new(ServerConfig.Twitch);
    public ElevenLabs elevenlabs = new(ServerConfig.ElevenLabs);
    public DiscordBot discord = new(ServerConfig.Discord);
    public ChatGpt chatgpt = new(ServerConfig.ChatGpt);
    public ObsClient obs = new(ServerConfig.Obs);
    public SpeechToText speech = new(ServerConfig.Speech);
    public ImageTextReader imageText = new TesseractImageReader();
    public HttpClient web = new();
    public FileGenerator file = new();
    public ScreenCapturer screen = new();

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

    public async Task<string> TakeAndUploadScreenshot()
    {
        var img = screen.TakeScreenshot();
        var fileUrl = await UploadImage(img);
        return fileUrl;
    }

    public async Task<string> UploadImage(string imagePath)
    {
        var fileUrl = await discord.UploadFile(imagePath);
        return fileUrl;
    }

    public void SetAssistant(string name)
    {
        var newAssistant = Assistants.Find((assistant) => string.Equals(assistant.Name, name, StringComparison.InvariantCultureIgnoreCase));
        if (newAssistant != null)
        {
            Narrator = newAssistant;
            log.Info($"Set Narrator to {name}");
        }
        else
        {
            log.Error($"Could not set Narrator to {name}");
        }
    }

    public void SetNarrator(string name)
    {
        var newAssistant = Assistants.Find((assistant) => string.Equals(assistant.Name, name, StringComparison.InvariantCultureIgnoreCase));
        if (newAssistant != null)
        {
            Assistant = newAssistant;
            log.Info($"Set Assistant to {name}");
        }
        else
        {
            log.Error($"Could not set Assistant to {name}");
        }
    }

    private Server()
    {

    }
}
