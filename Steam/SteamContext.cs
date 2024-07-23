using Steam.Models.SteamPlayer;

namespace TwitchBot.Steam
{
    public record SteamPlaytime(
        TimeSpan Last2Weeks,
        TimeSpan Forever
     );

    public record SteamContext(
        string UserName,
        string Game,
        SteamPlaytime Playtime,
        int CurrentPlayers,
        IReadOnlyCollection<PlayerAchievementModel> Achievements
    )
    {
        public static SteamContext Empty = new SteamContext(
            UserName: "",
            Game: "",
            Playtime: new(TimeSpan.Zero, TimeSpan.Zero),
            CurrentPlayers: 0,
            Achievements: new List<PlayerAchievementModel>()
        );
        public bool IsEmpty()
        {
            return string.IsNullOrEmpty(Game);
        }

        public List<PlayerAchievementModel> GetNewAchievements(SteamContext newContext)
        {
            var oldContext = this;
            if (oldContext.Game != newContext.Game)
            {
                return new();
            }

            var oldAchievements = oldContext.Achievements
                .Where((achievement) => { return achievement.Achieved == 1; })
                .Select((achievement) => { return achievement.Name; })
                .ToList();
            var newAchievements = newContext.Achievements
                .Where((achievement) => { 
                    return achievement.Achieved == 1 
                    && !oldAchievements.Contains(achievement.Name); 
                });

            return newAchievements.ToList();
        }
    }
}
