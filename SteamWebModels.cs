using Playnite.SDK.Data;
using System.Collections.Generic;

namespace SteamFriendsFullscreen
{
    public class SteamFriend
    {
        [SerializationPropertyName("steamid")]
        public string SteamId { get; set; }

        [SerializationPropertyName("relationship")]
        public string Relationship { get; set; }

        [SerializationPropertyName("friend_since")]
        public long FriendSince { get; set; }
    }

    public class GetFriendListResponseRoot
    {
        [SerializationPropertyName("friendslist")]
        public FriendsList FriendsList { get; set; }
    }

    public class FriendsList
    {
        [SerializationPropertyName("friends")]
        public List<SteamFriend> Friends { get; set; }
    }

    public class SteamPlayerSummary
    {
        [SerializationPropertyName("steamid")]
        public string SteamId { get; set; }

        [SerializationPropertyName("personaname")]
        public string PersonaName { get; set; }

        [SerializationPropertyName("personastate")]
        public int PersonaState { get; set; }

        [SerializationPropertyName("gameextrainfo")]
        public string GameExtraInfo { get; set; }

        [SerializationPropertyName("avatarfull")]
        public string AvatarFull { get; set; }
    }

    public class GetPlayerSummariesResponseRoot
    {
        [SerializationPropertyName("response")]
        public PlayerSummariesResponse Response { get; set; }
    }

    public class PlayerSummariesResponse
    {
        [SerializationPropertyName("players")]
        public List<SteamPlayerSummary> Players { get; set; }
    }

    public class ResolveVanityUrlResponseRoot
    {
        [SerializationPropertyName("response")]
        public ResolveVanityUrlResponse Response { get; set; }
    }

    public class ResolveVanityUrlResponse
    {
        [SerializationPropertyName("success")]
        public int Success { get; set; }

        [SerializationPropertyName("steamid")]
        public string SteamId { get; set; }

        [SerializationPropertyName("message")]
        public string Message { get; set; }
    }


    public class FriendPresenceDto
    {
        public string name { get; set; }
        public string state { get; set; }   
        public string game { get; set; }    
        public string steamid { get; set; }
        public string avatar { get; set; }  
    }
}
