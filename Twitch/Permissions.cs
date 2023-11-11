using Octokit;

namespace TwitchBot.Twitch
{
    enum PermissionGroup
    {
        Admin,
        Friend,
        User,
        Mod,
        Moose,

    }
    internal static class Permissions
    {
        public static bool IsUserInGroup(string user, PermissionGroup group)
        {
            if (group == PermissionGroup.User) { return true; }

            return Group.GetValueOrDefault(group, new()).Contains(user.ToLower());
        }

        public static readonly List<string> Admin = new()
        {
            "thatonesix",
            "cursedmoose",
        };

        public static readonly List<string> Moose = new()
        {
            "cursedmoose",
        };

        public static readonly Dictionary<PermissionGroup, List<string>> Group = new()
        {
            { PermissionGroup.Admin, Admin },
            { PermissionGroup.Moose, Moose }
        };


    }
}
