using OpenAI;

namespace TwitchBot.ChatGpt
{
    internal class ChatGptUsage
    {
        internal record struct TokenUsage(
            int tokens_used = 0,
            int lowest = 0,
            int highest = 0,
            int requests_made = 0
        );

        internal static TokenUsage prompts = new TokenUsage();
        internal static TokenUsage completions = new TokenUsage();
        internal static TokenUsage total = new TokenUsage();

        public void recordUsage(Usage usage)
        {
            if (usage.PromptTokens != null)
            {
                int tokens = (int) usage.PromptTokens;
                recordUsage(ref prompts, tokens);
            }
            if (usage.CompletionTokens != null)
            {
                int tokens = (int) usage.CompletionTokens;
                recordUsage(ref completions, tokens);
            }
            if (usage.TotalTokens != null)
            {
                int tokens = (int) usage.TotalTokens;
                recordUsage(ref total, tokens);
            }
        }

        private void recordUsage(ref TokenUsage stat, int tokensUsed)
        {
            stat.tokens_used += tokensUsed;
            stat.requests_made++;
            stat.lowest = stat.lowest == 0 ? tokensUsed : Math.Min(prompts.lowest, tokensUsed);
            stat.highest = Math.Max(prompts.highest, tokensUsed);
        }
    }
}
