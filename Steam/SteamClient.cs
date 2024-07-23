using Steam.Models.SteamCommunity;
using Steam.Models.SteamPlayer;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Utilities;
using System.Runtime.InteropServices;
using TwitchBot.Config;

namespace TwitchBot.Steam
{
    public class SteamClient
    {
        readonly Logger log = new("Steam");
        bool Enabled = false;

        SteamWebInterfaceFactory ApiFactory = new SteamWebInterfaceFactory(SteamConfig.API_KEY);
        HttpClient HttpClient;
        SteamUser User;
        SteamUserStats UserStats;
        PlayerService PlayerService;
        IReadOnlyCollection<OwnedGameModel>? OwnedGames;


        public SteamClient(bool enabled = true)
        {
            Enabled = enabled;
            HttpClient = new();
            User = ApiFactory.CreateSteamWebInterface<SteamUser>(HttpClient);
            UserStats = ApiFactory.CreateSteamWebInterface<SteamUserStats>(HttpClient);
            PlayerService = ApiFactory.CreateSteamWebInterface<PlayerService>(HttpClient);
        }

        public async Task<SteamContext> GetCurrentSteamContext()
        {
            var userInfo = await GetUserInfo();

            if (string.IsNullOrEmpty(userInfo.PlayingGameName))
            {
                return SteamContext.Empty;
            }

            var currentGameName = userInfo.PlayingGameName;
            var currentGameId = uint.Parse(userInfo.PlayingGameId);

            var achievements = Task.Run(() => GetAchievementsForGame(currentGameId));
            var currentPlayers = Task.Run(() => GetNumberOfCurrentPlayers(currentGameId));
            var playtime = Task.Run(() => GetPlaytime(currentGameId));
            await Task.WhenAll(achievements, currentPlayers, playtime);

            return new SteamContext(
                UserName: SteamConfig.STEAM_USER,
                Game: currentGameName,
                Playtime: await playtime,
                CurrentPlayers: await currentPlayers,
                Achievements: await achievements
            );
        }

        public async Task<PlayerSummaryModel> GetUserInfo()
        {
            var playerSummary = await User.GetPlayerSummaryAsync(SteamConfig.STEAM_USER_ID);
            return playerSummary.Data;
        }

        public async Task<IReadOnlyCollection<PlayerAchievementModel>> GetAchievementsForGame(string appId)
        {
            if (uint.TryParse(appId, out uint realAppId))
            {
                return await GetAchievementsForGame(realAppId);
            }
            else
            {
                log.Error($"Could not get AchievementsForGame as{appId} could not be parsed to a uint.");
                return new List<PlayerAchievementModel>();
            }
        }

        public async Task<IReadOnlyCollection<PlayerAchievementModel>> GetAchievementsForGame(uint appId)
        {
            var stats = await UserStats.GetPlayerAchievementsAsync(appId, SteamConfig.STEAM_USER_ID);
            return stats.Data.Achievements;
        }

        public async Task<int> GetNumberOfCurrentPlayers(string appId)
        {
            if (uint.TryParse(appId, out uint realAppId))
            {
                return await GetNumberOfCurrentPlayers(realAppId);
            }
            else
            {
                log.Error($"Could not get NumCurrentPlayers as {appId} could not be parsed to a uint.");
                return 0;
            }
        }

        public async Task<int> GetNumberOfCurrentPlayers(uint appId)
        {
            var currentPlayers = await UserStats.GetNumberOfCurrentPlayersForGameAsync(appId);
            return (int)currentPlayers.Data;
        }

        public async Task<IReadOnlyCollection<RecentlyPlayedGameModel>> GetRecentlyPlayedGameInfo()
        {
            var recentGames = await PlayerService.GetRecentlyPlayedGamesAsync(SteamConfig.STEAM_USER_ID);
            return recentGames.Data.RecentlyPlayedGames;
        }

        public async Task<SteamPlaytime> GetPlaytime(uint appId)
        {
            var ownedGames = await GetOwnedGames();
            var requestedGame = ownedGames.Where((game) => { return game.AppId == appId; }).First();
            var last2Weeks = requestedGame.PlaytimeLastTwoWeeks.HasValue ? requestedGame.PlaytimeLastTwoWeeks.Value : TimeSpan.Zero;
            return new SteamPlaytime(
                Last2Weeks: last2Weeks,
                Forever: requestedGame.PlaytimeForever
            );
        }

        public async Task<IReadOnlyCollection<OwnedGameModel>> GetOwnedGames()
        {
            if (OwnedGames != null)
            {
                return OwnedGames;
            }
            else
            {
                var ownedGames = await PlayerService.GetOwnedGamesAsync(SteamConfig.STEAM_USER_ID, true);
                OwnedGames = ownedGames.Data.OwnedGames;
                return OwnedGames;
            }
        }
    }
}
