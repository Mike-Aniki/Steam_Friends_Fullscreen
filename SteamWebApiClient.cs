using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SteamFriendsFullscreen
{
    public class SteamWebApiClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly HttpClient http;

        public SteamWebApiClient()
        {
            http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public async Task<List<string>> GetFriendSteamIdsAsync(string apiKey, string steamId64)
        {
            var url =
                $"https://api.steampowered.com/ISteamUser/GetFriendList/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}&relationship=friend";

            var json = await http.GetStringAsync(url).ConfigureAwait(false);
            var root = Serialization.FromJson<GetFriendListResponseRoot>(json);

            return root?.FriendsList?.Friends?
                .Where(f => string.Equals(f.Relationship, "friend", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.SteamId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList()
                ?? new List<string>();
        }

        public async Task<List<SteamPlayerSummary>> GetPlayerSummariesAsync(string apiKey, IEnumerable<string> steamIds)
        {
            var ids = steamIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList() ?? new List<string>();
            if (ids.Count == 0)
            {
                return new List<SteamPlayerSummary>();
            }

            var all = new List<SteamPlayerSummary>();
            foreach (var chunk in Chunk(ids, 100))
            {
                var joined = string.Join(",", chunk);
                var url =
                    $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={Uri.EscapeDataString(apiKey)}&steamids={Uri.EscapeDataString(joined)}";

                var json = await http.GetStringAsync(url).ConfigureAwait(false);
                var root = Serialization.FromJson<GetPlayerSummariesResponseRoot>(json);

                var players = root?.Response?.Players;
                if (players != null)
                {
                    all.AddRange(players);
                }
            }

            return all;
        }

        public async Task<string> ResolveVanityUrlAsync(string apiKey, string vanity)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(vanity))
            {
                return null;
            }

            var url =
                $"https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/?key={Uri.EscapeDataString(apiKey)}&vanityurl={Uri.EscapeDataString(vanity)}";

            try
            {
                var json = await http.GetStringAsync(url).ConfigureAwait(false);
                var root = Serialization.FromJson<ResolveVanityUrlResponseRoot>(json);

                if (root?.Response?.Success == 1 &&
                    !string.IsNullOrWhiteSpace(root.Response.SteamId))
                {
                    return root.Response.SteamId;
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"ResolveVanityURL failed for '{vanity}'.");
                return null;
            }
        }


        private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
            {
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
            }
        }
    }
}
