using System;
using System.Collections.Generic;

namespace SteamFriendsFullscreen
{
    public class RecentGameDto
    {
        public int appid { get; set; }
        public string name { get; set; }
        public int playtime2WeeksMinutes { get; set; }
        public int playtimeForeverMinutes { get; set; }

        public string playtime2WeeksDisplay { get; set; }
        public string playtimeForeverDisplay { get; set; }
        public string headerImageUrl { get; set; }
    }

    public class FriendProfileDto
    {
        public string steamid { get; set; }
        public string name { get; set; }
        public string avatar { get; set; }

        public string state { get; set; }
        public string stateLoc { get; set; }
        public string game { get; set; }

        public bool isProfilePublic { get; set; }

        public DateTime? lastLogoffUtc { get; set; }
        public DateTime? friendSinceUtc { get; set; }

        public int steamLevel { get; set; }
        public int badgesCount { get; set; }
        public int recentPlaytime2WeeksMinutes { get; set; }
        public string recentPlaytime2WeeksDisplay { get; set; }

        public List<RecentGameDto> recentGames { get; set; } = new List<RecentGameDto>();
    }

    public class CachedFriendProfile
    {
        public string steamid { get; set; }
        public DateTime cachedAtUtc { get; set; }
        public FriendProfileDto profile { get; set; }
    }
}