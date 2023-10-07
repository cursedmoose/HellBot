// See https://aka.ms/new-console-template for more information
using TwitchBot;
using TwitchBot.ElevenLabs;
using TwitchBot.Discord;
using TwitchBot.ChatGpt;
using System.Globalization;
using System.Text.RegularExpressions;
using TwitchBot.Assistant;
using TwitchBot.Assistant.Polls;

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
        Log($"Used {info.character_count} / {info.character_limit} characters.");
        Log($"This instance has used {info.character_count - server.elevenlabs.charactersStartedAt} characters.");
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
            Log(await server.chatgpt.getResponseText(prompt));
        }
        catch (Exception e)
        {
            Log($"Exceptioned out. Got {e.Message}");
        }
    }
    else if (next.Contains("chat"))
    {
        var chatters = await Server.Instance.twitch.GetChatters();
        chatters.ForEach(Log);
    }
    else if (next.Contains("poll"))
    {
        var prompt1 = "make a 3 option poll with a title. limit the options to 5 words";
        var prompt2 = "make a poll with 3 options and a title. limit the options to 5 words";
        var prompt3 = "fill out the following poll\r\nTitle:\r\nOption 1:\r\nOption 2:\r\nOption 3:";
        string response = await server.chatgpt.getResponseText(prompt3);
        Log(response);

        Poll poll = PollParser.parsePoll(response);

        Log(poll.Title);
        foreach (string option in poll.Choices)
        {
            Log($"x?: {option}");
        }
    }
    else if(next.Contains("make"))
    {
        await server.Assistant.CreatePoll();
        /*
        await Server.Instance.twitch.CreatePoll(
            title: "this is an automatic test",
            choices: new List<string>()
            {
                "option 1",
                "option 2"
            });
        */
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
    public DiscordBot discord = new(false);
    public ChatGpt chatgpt = new(GLOBAL_ENABLE);
    public HttpClient web = new();


    private static readonly Lazy<Server> lazy = new Lazy<Server>(() => new Server());
    public static Server Instance { get { return lazy.Value; } }

    public async void saveAs(string webUrl, string filename, string fileExtension = "")
    {
        var response = await web.GetAsync(webUrl);
        using (var fs = new FileStream(Path.Combine("images", filename + fileExtension), FileMode.Create))
        {
            await response.Content.CopyToAsync(fs);
        }
    }

    private Server()
    {

    }
}
