// See https://aka.ms/new-console-template for more information
using TwitchBot;
using TwitchBot.ElevenLabs;
using TwitchBot.Discord;
using TwitchBot.ChatGpt;
using System.Globalization;
using System.Text.RegularExpressions;
using TwitchBot.Assistant;
using TwitchBot.OBS;
using TwitchBot.OBS.Scene;

var multiOut = new MultiWriter(Console.Out, $"logs/{DateTime.Now.ToString("yyyy-MM-dd")}.txt");
Console.SetOut(multiOut);
void Log(string message)
{
    var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
    Console.WriteLine($"{timestamp} [MAIN] {message}");
}

Log("~~~~~~~~~~~~~");
Log("Hello, World!");

Server server = Server.Instance;

while (true)
{
    var next = Console.ReadLine();
    if (next == null || next.Trim().Length == 0)
    {
        continue;
    }
    else if (next == "exit")
    {
        server.twitch.stop();
        server.web.Dispose();
        server.obs.Disconnect();
        break;
    }
    else if (next == "health")
    {
        Server.Instance.twitch.HealthCheck();
    }
    else if (next == "usage")
    {
        var info = server.elevenlabs.getUserSubscriptionInfo();
        // Log(info.ToString());
        Log($"[ElevenLabs] Used {info.character_count} / {info.character_limit} characters.");
        Log($"[ElevenLabs] This instance has used {info.character_count - server.elevenlabs.charactersStartedAt} characters.");

        server.chatgpt.getUsage();
    }
    else if (next == "okok")
    {
        Log($"Presence: {server.discord.getPresence()}");
    }
    else if (next.Contains("dalle"))
    {
        try
        {
            var prompt = next.Substring(5).Trim();
            Log($"Generating image for : \"{prompt}\"");
            var image = await server.chatgpt.getImage(prompt);
            Log($"{image}");
        }
        catch (Exception e)
        {
            Log($"Exceptioned out. Got {e.Message}");
        }
    }
    else if (next.Contains("gpt"))
    {
        try
        {
            var prompt = next.Substring(3).Trim();
            Log($"Generating words for : \"{prompt}\"");
            Log(await server.chatgpt.getResponseText(server.Assistant.Persona, prompt));
        }
        catch (Exception e)
        {
            Log($"Exceptioned out. Got {e.Message}");
        }
    }
    else if (next.Contains("chat"))
    {
        var chatters = await Server.Instance.twitch.GetChatterNames();
        chatters.ForEach(Log);
    }
    else if (next.Contains("start"))
    {
        server.Assistant.StartAI();
    }
    else if (next.Contains("stop"))
    {
        await server.Assistant.StopAI();
    }
    else if(next.Contains("create"))
    {
        // (server.Assistant as Sheogorath).CreateReward();
        (server.Assistant as Sheogorath).PaintPicture();
    }
    else if (next.Contains("delete"))
    {
        // (server.Assistant as Sheogorath).DeleteReward();
    }
    else if (next.Contains("clean"))
    {
        server.Assistant.CleanUp();
    }
    else if (next.Contains("obs"))
    {
        server.obs.GetActiveSource();
    }
    else if (next.Contains("on"))
    {
        server.obs.EnableScene(ObsScenes.Sheogorath);
    }
    else if (next.Contains("off"))
    {
        server.obs.DisableScene(ObsScenes.Sheogorath);
    }
    else
    {
        //Console.WriteLine(PlayTts.cleanStringForTts(next));
        // server.elevenlabs.playTts(next, VoiceProfiles.EsoProphet);
        //var cleanedMessage = Server.WEBSITE_REGEX.Replace(next, "");
        //Log(cleanedMessage);

        // var image = await server.chatgpt.getImage(next);
        // server.chatgpt.getResponse(next);
    }
}

public class Server
{
    public static readonly CultureInfo LOG_FORMAT = new CultureInfo("en-GB");
    public static readonly Regex WEBSITE_REGEX = new Regex("[(http(s)?):\\/\\/(www\\.)?a-zA-Z0-9@:%._\\+~#=]{2,256}\\.[a-z]{2,6}\\b([-a-zA-Z0-9@:%_\\+.~#?&//=]*)", RegexOptions.IgnoreCase);
    public static readonly Regex EMOTE_REGEX = new Regex("cursed99");

    static bool GLOBAL_ENABLE = true;

    public Assistant Assistant = new Sheogorath();

    public TwitchIrcBot twitch = new(GLOBAL_ENABLE);
    public ElevenLabs elevenlabs = new(GLOBAL_ENABLE);
    public DiscordBot discord = new(GLOBAL_ENABLE);
    public ChatGpt chatgpt = new(GLOBAL_ENABLE);
    public ObsClient obs = new(GLOBAL_ENABLE);
    public HttpClient web = new();


    private static readonly Lazy<Server> lazy = new Lazy<Server>(() => new Server());
    public static Server Instance { get { return lazy.Value; } }

    public async void saveImageAs(string webUrl, string filename, string fileExtension = "")
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

    public async Task<string> shortenUrl(string longUrl)
    {
        // "https://tinyurl.com/app/api/url/create"
        var response = await web.GetAsync($"https://tinyurl.com/api-create.php?url={longUrl}");

        return await response.Content.ReadAsStringAsync();
    }

    private string RemoveInvalidChars(string filename)
    {
        return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
    }

    private Server()
    {

    }
}
