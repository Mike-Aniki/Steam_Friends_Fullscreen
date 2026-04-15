using Playnite.SDK.Data;
using System;
using System.IO;

namespace SteamFriendsFullscreen
{
    public class FriendProfileCacheStore
    {
        private readonly string cacheDir;

        public FriendProfileCacheStore(string pluginUserDataPath)
        {
            cacheDir = Path.Combine(pluginUserDataPath, "FriendProfilesCache");
            Directory.CreateDirectory(cacheDir);
        }

        public CachedFriendProfile TryLoad(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                return null;
            }

            var path = GetFilePath(steamId);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return Serialization.FromJson<CachedFriendProfile>(json);
            }
            catch
            {
                return null;
            }
        }

        public void Save(CachedFriendProfile entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.steamid) || entry.profile == null)
            {
                return;
            }

            try
            {
                var path = GetFilePath(entry.steamid);
                var json = Serialization.ToJson(entry);
                File.WriteAllText(path, json);
            }
            catch
            {
            }
        }

        public void Delete(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                return;
            }

            try
            {
                var path = GetFilePath(steamId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private string GetFilePath(string steamId)
        {
            return Path.Combine(cacheDir, steamId + ".json");
        }
    }
}