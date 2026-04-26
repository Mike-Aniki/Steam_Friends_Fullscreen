using Playnite.SDK.Data;
using System;
using System.Collections.Generic;

namespace SteamFriendsFullscreen
{
    public class FriendAchievementFeedCache
    {
        public string LastUpdatedUtc { get; set; }
        public List<FriendAchievementFeedEntry> Entries { get; set; } = new List<FriendAchievementFeedEntry>();
    }

    public class FriendAchievementFeedEntry
    {
        public string AchievementApiName { get; set; }
        public string AchievementDisplayName { get; set; }
        public string AchievementDescription { get; set; }

        public int AppId { get; set; }
        public Guid? PlayniteGameId { get; set; }

        public string GameName { get; set; }

        public string FriendPersonaName { get; set; }
        public string FriendAvatarUrl { get; set; }
        public string FriendSteamId { get; set; }

        public string FriendAchievementIcon { get; set; }

        public string FriendUnlockTimeUtc { get; set; }

        public bool IsRevealed { get; set; }
        public bool HideAchievementsLockedForSelf { get; set; }

        public string SelfAchievementIcon { get; set; }
        public string SelfUnlockTime { get; set; }
    }

    public class RecentFriendAchievementDto
    {
        public string achievementApiName { get; set; }
        public string achievementDisplayName { get; set; }
        public string achievementDescription { get; set; }

        public int appid { get; set; }
        public Guid? playniteGameId { get; set; }

        public string gameName { get; set; }
        public string icon { get; set; }

        public DateTime? unlockTimeUtc { get; set; }
        public string unlockTimeDisplay { get; set; }

        public string rarity { get; set; } // réservé pour plus tard si tu enrichis FAF
    }
}