using System.Net.Http.Json;

namespace TwitchBot.Assistant.Polls
{
    internal static class TopicGenerator
    {
        const string STORY_SHACK_API = "https://story-shack-cdn-v2.glitch.me/generators/random-topic-generator?count=6";

        record Topic(string Name);
        record TopicList(List<Topic> Data);
        

        public static async Task<string> GenerateTopic()
        {
            var topics = await Server.Instance.web.GetFromJsonAsync(STORY_SHACK_API, typeof(TopicList)) as TopicList;

            return (topics == null) ? "" : topics.Data[0].Name;
        }
    }
}
