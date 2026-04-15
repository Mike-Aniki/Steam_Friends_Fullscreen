using Playnite.SDK.Data;
using System.Collections.Generic;

namespace SteamFriendsFullscreen
{
    public class GetRecentlyPlayedGamesResponseRoot
    {
        [SerializationPropertyName("response")]
        public RecentlyPlayedGamesResponse Response { get; set; }
    }

    public class RecentlyPlayedGamesResponse
    {
        [SerializationPropertyName("total_count")]
        public int TotalCount { get; set; }

        [SerializationPropertyName("games")]
        public List<SteamRecentlyPlayedGame> Games { get; set; }
    }

    public class SteamRecentlyPlayedGame
    {
        [SerializationPropertyName("appid")]
        public int AppId { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("playtime_2weeks")]
        public int Playtime2Weeks { get; set; }

        [SerializationPropertyName("playtime_forever")]
        public int PlaytimeForever { get; set; }
    }
}