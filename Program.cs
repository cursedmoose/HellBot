﻿using TwitchBot;
using TwitchBot.Twitch;
using TwitchBot.ElevenLabs;
using TwitchBot.Discord;
using TwitchBot.ChatGpt;
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
using TwitchBot.EyeTracking;
using TwitchBot.EEG;
using TwitchBot.AWS;
using TwitchBot.Elgato;
using TwitchBot.Steam;
using TwitchBot.Ollama;
using TwitchBot.CommandLine.Commands.Ollama;

Directory.CreateDirectory("logs");
Directory.CreateDirectory("images/screenshots");
Directory.CreateDirectory("etc");

var multiOut = new MultiWriter(Console.Out, $"logs/{DateTime.Now:yyyy-MM-dd}.txt");
Console.SetOut(multiOut);
var log = new Logger("MAIN");
Logger.Level = ConsoleLogLevel.Info;

log.Info("~~~~~~~~~~~~~");
log.Info("Hello, World!");
log.Info($"Logging configured to {Logger.Level}");

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
    new Screenshot(),
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
    new PrintSources(),
    #endregion
    #region Ollama Commands
    new OllamaChat(),
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
    public static ServerMode CurrentServerMode = ServerMode.PRODUCTION;

    public static bool PRODUCTION { get { return (CurrentServerMode & ServerMode.PRODUCTION) == ServerMode.PRODUCTION; } }
    public static bool DEVELOPMENT { get { return (CurrentServerMode & ServerMode.DEVELOPMENT) == ServerMode.DEVELOPMENT; } }
    public static bool EXPERIMENTAL { get { return (CurrentServerMode & ServerMode.EXPERIMENTAL) == ServerMode.EXPERIMENTAL; } }

    public static readonly bool ENABLED = true;
    public static readonly bool DISABLED = false;
}

public enum ServerMode
{
    NONE = 0,
    EXPERIMENTAL = 1,
    DEVELOPMENT = 2,
    PRODUCTION = 4
};

public class Server
{
    public static readonly Regex WEBSITE_REGEX = new("[(http(s)?):\\/\\/(www\\.)?a-zA-Z0-9@:%._\\+~#=]{2,256}\\.[a-z]{2,6}\\b([-a-zA-Z0-9@:%_\\+.~#?&//=]*)", RegexOptions.IgnoreCase);
    public static readonly Regex EMOTE_REGEX = new("cursed99");

    private Logger log = new("Server");

    static Assistant Sheogorath = new Sheogorath();
    static Assistant Werner = new Werner();
    static Assistant God = new God();

    public Assistant Assistant = Sheogorath;
    public Assistant Narrator = Werner;

    public List<Assistant> Assistants = new()
    {
        Sheogorath,
        Werner,
        God
    };

    public AwsClient aws = new(ServerConfig.ENABLED);
    public ChatGpt chatgpt = new(ServerConfig.PRODUCTION);
    public DiscordBot discord = new(ServerConfig.PRODUCTION);
    public ElevenLabs elevenlabs = new(ServerConfig.PRODUCTION);
    public ObsClient obs = new(ServerConfig.PRODUCTION);
    public SteamClient steam = new(ServerConfig.PRODUCTION);
    public StreamDeck streamDeck = new(ServerConfig.PRODUCTION);
    public TobiiEyeTracker eyetracker = new(ServerConfig.PRODUCTION);
    public TwitchIrcBot twitch = new(ServerConfig.PRODUCTION);

    public Ollama ollama = new(ServerConfig.DEVELOPMENT);

    public MicrophoneListener micListener = new(ServerConfig.EXPERIMENTAL);
    public MuseMonitor brain = new(ServerConfig.EXPERIMENTAL);
    public SpeechToText speech = new(ServerConfig.EXPERIMENTAL);

    public HttpClient web = new();
    public FileGenerator file = new();
    public ScreenCapturer screen = new();
    public ImageTextReader imageText = new TesseractImageReader();

    private static readonly Lazy<Server> lazy = new(() => new Server());
    public static Server Instance { get { return lazy.Value; } }

    public async Task<string> ShortenUrl(string longUrl)
    {
        var response = await web.GetAsync($"https://tinyurl.com/api-create.php?url={longUrl}");
        return await response.Content.ReadAsStringAsync();
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
            Assistant = newAssistant;
            log.Info($"Set Assistant to {name}");
        }
        else
        {
            log.Error($"Could not set Assistant to {name}");
        }
    }

    public void SetNarrator(string name)
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

    private Server()
    {

    }
}

public static class Extensions
{
    public static T RandomElement<T>(this List<T> list)
    {
        var randomIndex = new Random().Next(list.Count);
        return list[randomIndex];
    }
} 
