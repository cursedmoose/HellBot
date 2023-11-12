using TwitchBot.Config;

namespace TwitchBot.Twitch
{
    public enum PermissionGroup
    {
        Admin,
        User,
        Moose,
    }
    public static class Permissions
    {
        public static bool IsUserInGroup(string user, PermissionGroup group)
        {
            if (group == PermissionGroup.User) { return true; }

            return Group.GetValueOrDefault(group, new()).Contains(user.ToLower());
        }

        public static readonly List<string> Admin = new()
        {
            // TwitchConfig.Admins.Moose,
            TwitchConfig.Admins.Six,
            TwitchConfig.Admins.Sas,
            TwitchConfig.Admins.Dlique,
            TwitchConfig.Admins.Elise1,
            TwitchConfig.Admins.Elise2
        };

        public static readonly List<string> Moose = new()
        {
            TwitchConfig.Admins.Moose
        };

        public static readonly Dictionary<PermissionGroup, List<string>> Group = new()
        {
            { PermissionGroup.Admin, Admin },
            { PermissionGroup.Moose, Moose }
        };


    }
}
