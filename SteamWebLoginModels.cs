using Playnite.SDK.Data;
using System;
using System.Collections.Generic;

namespace SteamFriendsFullscreen
{
    public enum SteamWebLoginState
    {
        Disconnected = 0,
        Connecting = 1,
        WaitingForScan = 2,
        Connected = 3,
        Refreshing = 4,
        Error = 5
    }

    public enum SteamActiveDataSource
    {
        None = 0,
        WebLogin = 1,
        ApiKeyFallback = 2
    }

    public sealed class SteamWebLoginSession
    {
        public string AccountName { get; set; }
        public string SteamId64 { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime AccessTokenExpiresUtc { get; set; }
    }

    public class WebLoginFriendListRoot
    {
        [SerializationPropertyName("response")]
        public WebLoginFriendListResponse Response { get; set; }

        [SerializationPropertyName("friendslist")]
        public FriendsList FriendsList { get; set; }

        [SerializationPropertyName("friends")]
        public List<SteamFriend> Friends { get; set; }
    }

    public class WebLoginFriendListResponse
    {
        [SerializationPropertyName("friendslist")]
        public FriendsList FriendsList { get; set; }

        [SerializationPropertyName("friends")]
        public List<SteamFriend> Friends { get; set; }
    }

    public class WebLoginPlayerSummariesRoot
    {
        [SerializationPropertyName("response")]
        public PlayerSummariesResponse Response { get; set; }

        [SerializationPropertyName("players")]
        public List<SteamPlayerSummary> Players { get; set; }
    }
}
