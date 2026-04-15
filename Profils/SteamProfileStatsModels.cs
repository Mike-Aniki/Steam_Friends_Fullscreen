using Playnite.SDK.Data;
using System.Collections.Generic;

namespace SteamFriendsFullscreen
{
    public class GetSteamLevelResponseRoot
    {
        [SerializationPropertyName("response")]
        public SteamLevelResponse Response { get; set; }
    }

    public class SteamLevelResponse
    {
        [SerializationPropertyName("player_level")]
        public int PlayerLevel { get; set; }
    }

    public class GetBadgesResponseRoot
    {
        [SerializationPropertyName("response")]
        public SteamBadgesResponse Response { get; set; }
    }

    public class SteamBadgesResponse
    {
        [SerializationPropertyName("badges")]
        public List<SteamBadge> Badges { get; set; }
    }

    public class SteamBadge
    {
        [SerializationPropertyName("badgeid")]
        public int BadgeId { get; set; }

        [SerializationPropertyName("level")]
        public int Level { get; set; }

        [SerializationPropertyName("appid")]
        public int? AppId { get; set; }
    }
}