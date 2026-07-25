using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SteamFriendsFullscreen
{
    public enum SteamWebLoginApiErrorKind
    {
        Unauthorized,
        RateLimited,
        Timeout,
        Network,
        SteamUnavailable,
        InvalidResponse,
        HttpError
    }

    public sealed class SteamWebLoginApiException : Exception
    {
        public SteamWebLoginApiErrorKind Kind { get; }
        public HttpStatusCode? StatusCode { get; }

        public SteamWebLoginApiException(
            SteamWebLoginApiErrorKind kind,
            string message,
            HttpStatusCode? statusCode = null,
            Exception innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
            StatusCode = statusCode;
        }
    }

    public sealed class SteamWebLoginApiClient : IDisposable
    {
        private readonly HttpClient http;

        public SteamWebLoginApiClient()
        {
            http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
        }

        public async Task<List<SteamFriend>> GetFriendsAsync(
            string accessToken,
            string steamId64)
        {
            var url = BuildUrl(
                "IFriendsListService/GetFriendsList/v1/",
                accessToken,
                "steamid=" + Escape(steamId64) + "&relationship=friend");

            var json = await GetStringCheckedAsync(url).ConfigureAwait(false);
            var root = DeserializeOrThrow<WebLoginFriendListRoot>(json);
            if (root.Response == null && root.FriendsList == null && root.Friends == null)
            {
                throw new SteamWebLoginApiException(
                    SteamWebLoginApiErrorKind.InvalidResponse,
                    "Steam returned a friend-list response in an unknown format.");
            }

            var friends = root?.Response?.Friends
                ?? root?.Response?.FriendsList?.Friends
                ?? root?.FriendsList?.Friends
                ?? root?.Friends
                ?? new List<SteamFriend>();

            return friends
                .Where(f => f != null && !string.IsNullOrWhiteSpace(f.SteamId))
                .GroupBy(f => f.SteamId)
                .Select(g => g.First())
                .ToList();
        }

        public async Task<List<string>> GetFriendSteamIdsAsync(
            string accessToken,
            string steamId64)
        {
            var friends = await GetFriendsAsync(accessToken, steamId64).ConfigureAwait(false);
            return friends
                .Select(f => f.SteamId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
        }

        public async Task<List<SteamPlayerSummary>> GetPlayerSummariesAsync(
            string accessToken,
            IEnumerable<string> steamIds)
        {
            var ids = steamIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList() ?? new List<string>();

            if (ids.Count == 0)
            {
                return new List<SteamPlayerSummary>();
            }

            var all = new List<SteamPlayerSummary>();
            foreach (var chunk in Chunk(ids, 100))
            {
                var joined = string.Join(",", chunk);
                var url = BuildUrl(
                    "ISteamUserOAuth/GetUserSummaries/v2/",
                    accessToken,
                    "steamids=" + Escape(joined));

                var json = await GetStringCheckedAsync(url).ConfigureAwait(false);
                var root = DeserializeOrThrow<WebLoginPlayerSummariesRoot>(json);
                if (root.Response == null && root.Players == null)
                {
                    throw new SteamWebLoginApiException(
                        SteamWebLoginApiErrorKind.InvalidResponse,
                        "Steam returned a player-summary response in an unknown format.");
                }

                var players = root?.Response?.Players ?? root?.Players;
                if (players != null)
                {
                    all.AddRange(players);
                }
            }

            return all;
        }

        public async Task<List<SteamRecentlyPlayedGame>> GetRecentlyPlayedGamesAsync(
            string accessToken,
            string steamId64,
            int count = 3)
        {
            var safeCount = Math.Max(1, count);
            var url = BuildUrl(
                "IPlayerService/GetRecentlyPlayedGames/v1/",
                accessToken,
                "steamid=" + Escape(steamId64) + "&count=" + safeCount);

            var json = await GetStringCheckedAsync(url).ConfigureAwait(false);
            var root = DeserializeOrThrow<GetRecentlyPlayedGamesResponseRoot>(json);
            if (root.Response == null)
            {
                throw new SteamWebLoginApiException(
                    SteamWebLoginApiErrorKind.InvalidResponse,
                    "Steam returned recent-game data in an unknown format.");
            }
            return root.Response.Games ?? new List<SteamRecentlyPlayedGame>();
        }

        public async Task<List<SteamRecentlyPlayedGame>> GetAllRecentlyPlayedGamesAsync(
            string accessToken,
            string steamId64)
        {
            var url = BuildUrl(
                "IPlayerService/GetRecentlyPlayedGames/v1/",
                accessToken,
                "steamid=" + Escape(steamId64) + "&count=0");

            var json = await GetStringCheckedAsync(url).ConfigureAwait(false);
            var root = DeserializeOrThrow<GetRecentlyPlayedGamesResponseRoot>(json);
            if (root.Response == null)
            {
                throw new SteamWebLoginApiException(
                    SteamWebLoginApiErrorKind.InvalidResponse,
                    "Steam returned recent-game data in an unknown format.");
            }
            return root.Response.Games ?? new List<SteamRecentlyPlayedGame>();
        }

        public async Task<int> GetSteamLevelAsync(string accessToken, string steamId64)
        {
            var url = BuildUrl(
                "IPlayerService/GetSteamLevel/v1/",
                accessToken,
                "steamid=" + Escape(steamId64));

            var json = await GetStringCheckedAsync(url).ConfigureAwait(false);
            var root = DeserializeOrThrow<GetSteamLevelResponseRoot>(json);
            if (root.Response == null)
            {
                throw new SteamWebLoginApiException(
                    SteamWebLoginApiErrorKind.InvalidResponse,
                    "Steam returned Steam-level data in an unknown format.");
            }
            return root.Response.PlayerLevel;
        }

        public async Task<List<SteamBadge>> GetBadgesAsync(string accessToken, string steamId64)
        {
            var url = BuildUrl(
                "IPlayerService/GetBadges/v1/",
                accessToken,
                "steamid=" + Escape(steamId64));

            var json = await GetStringCheckedAsync(url).ConfigureAwait(false);
            var root = DeserializeOrThrow<GetBadgesResponseRoot>(json);
            if (root.Response == null)
            {
                throw new SteamWebLoginApiException(
                    SteamWebLoginApiErrorKind.InvalidResponse,
                    "Steam returned badge data in an unknown format.");
            }
            return root.Response.Badges ?? new List<SteamBadge>();
        }

        private static string BuildUrl(string method, string accessToken, string extraQuery)
        {
            var query = "access_token=" + Escape(accessToken);
            if (!string.IsNullOrWhiteSpace(extraQuery))
            {
                query += "&" + extraQuery;
            }

            return "https://api.steampowered.com/" + method + "?" + query;
        }

        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        private async Task<string> GetStringCheckedAsync(string url)
        {
            HttpResponseMessage response;
            try
            {
                response = await http.GetAsync(url).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                throw new SteamWebLoginApiException(
                    SteamWebLoginApiErrorKind.Timeout,
                    "The Steam WebLogin request timed out.",
                    innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                throw new SteamWebLoginApiException(
                    SteamWebLoginApiErrorKind.Network,
                    "The Steam WebLogin service could not be reached.",
                    innerException: ex);
            }

            using (response)
            {
                string body;
                try
                {
                    body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new SteamWebLoginApiException(
                        SteamWebLoginApiErrorKind.InvalidResponse,
                        "Steam returned an unreadable WebLogin response.",
                        response.StatusCode,
                        ex);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new SteamWebLoginApiException(
                        SteamWebLoginApiErrorKind.Unauthorized,
                        "The Steam WebLogin token was refused.",
                        response.StatusCode);
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new SteamWebLoginApiException(
                        SteamWebLoginApiErrorKind.RateLimited,
                        "Steam temporarily rate-limited WebLogin requests.",
                        response.StatusCode);
                }

                if ((int)response.StatusCode >= 500)
                {
                    throw new SteamWebLoginApiException(
                        SteamWebLoginApiErrorKind.SteamUnavailable,
                        "The Steam WebLogin service is temporarily unavailable.",
                        response.StatusCode);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new SteamWebLoginApiException(
                        SteamWebLoginApiErrorKind.HttpError,
                        "Steam returned HTTP " + (int)response.StatusCode + " (" + response.ReasonPhrase + ").",
                        response.StatusCode);
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    throw new SteamWebLoginApiException(
                        SteamWebLoginApiErrorKind.InvalidResponse,
                        "Steam returned an empty WebLogin response.",
                        response.StatusCode);
                }

                return body;
            }
        }

        private static T DeserializeOrThrow<T>(string json) where T : class
        {
            try
            {
                var value = Serialization.FromJson<T>(json);
                if (value == null)
                {
                    throw new InvalidOperationException("Deserialization returned null.");
                }

                return value;
            }
            catch (Exception ex)
            {
                throw new SteamWebLoginApiException(
                    SteamWebLoginApiErrorKind.InvalidResponse,
                    "Steam returned WebLogin data in an unexpected format.",
                    innerException: ex);
            }
        }

        private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
        {
            for (var i = 0; i < source.Count; i += size)
            {
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
            }
        }

        public void Dispose()
        {
            http.Dispose();
        }
    }
}
