using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SteamFriendsFullscreen
{
    public enum SteamWebApiErrorKind
    {
        InvalidApiKey,
        FriendsListPrivate,
        RateLimited,
        Timeout,
        Network,
        SteamUnavailable,
        InvalidResponse,
        HttpError
    }

    public sealed class SteamWebApiException : Exception
    {
        public SteamWebApiErrorKind Kind { get; }
        public HttpStatusCode? StatusCode { get; }

        public SteamWebApiException(
            SteamWebApiErrorKind kind,
            string message,
            HttpStatusCode? statusCode = null,
            Exception innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
            StatusCode = statusCode;
        }
    }

    public enum SteamConnectionTestCode
    {
        Success,
        SuccessNoFriends,
        MissingApiKey,
        InvalidApiKeyFormat,
        MissingProfile,
        InvalidProfileFormat,
        ProfileNotFound,
        InvalidApiKey,
        FriendsListPrivate,
        RateLimited,
        Timeout,
        Network,
        SteamUnavailable,
        InvalidResponse,
        ApiError,
        UnknownError
    }

    public sealed class SteamConnectionTestResult
    {
        public SteamConnectionTestCode Code { get; set; }
        public string SteamId64 { get; set; }
        public string PersonaName { get; set; }
        public int FriendCount { get; set; }

        public bool IsSuccess =>
            Code == SteamConnectionTestCode.Success ||
            Code == SteamConnectionTestCode.SuccessNoFriends;
    }

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

        public async Task<List<SteamFriend>> GetFriendsAsync(string apiKey, string steamId64)
        {
            var url =
                $"https://api.steampowered.com/ISteamUser/GetFriendList/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}&relationship=friend";

            var json = await GetStringCheckedAsync(url, isFriendListRequest: true).ConfigureAwait(false);
            var root = DeserializeOrThrow<GetFriendListResponseRoot>(json);

            return root?.FriendsList?.Friends?
                .Where(f => string.Equals(f.Relationship, "friend", StringComparison.OrdinalIgnoreCase))
                .Where(f => !string.IsNullOrWhiteSpace(f.SteamId))
                .GroupBy(f => f.SteamId)
                .Select(g => g.First())
                .ToList()
                ?? new List<SteamFriend>();
        }

        public async Task<List<string>> GetFriendSteamIdsAsync(string apiKey, string steamId64)
        {
            var friends = await GetFriendsAsync(apiKey, steamId64).ConfigureAwait(false);

            return friends
                .Select(f => f.SteamId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
        }

        public async Task<List<SteamRecentlyPlayedGame>> GetRecentlyPlayedGamesAsync(string apiKey, string steamId64, int count = 3)
        {
            try
            {
                var safeCount = Math.Max(1, count);

                var url =
                    $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}&count={safeCount}";

                var json = await http.GetStringAsync(url).ConfigureAwait(false);
                var root = Serialization.FromJson<GetRecentlyPlayedGamesResponseRoot>(json);

                return root?.Response?.Games ?? new List<SteamRecentlyPlayedGame>();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"GetRecentlyPlayedGames failed for '{steamId64}'.");
                return new List<SteamRecentlyPlayedGame>();
            }
        }

        public async Task<List<SteamRecentlyPlayedGame>> GetAllRecentlyPlayedGamesAsync(string apiKey, string steamId64)
        {
            try
            {
                var url =
                    $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}&count=0";

                var json = await http.GetStringAsync(url).ConfigureAwait(false);
                var root = Serialization.FromJson<GetRecentlyPlayedGamesResponseRoot>(json);

                return root?.Response?.Games ?? new List<SteamRecentlyPlayedGame>();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"GetAllRecentlyPlayedGames failed for '{steamId64}'.");
                return new List<SteamRecentlyPlayedGame>();
            }
        }

        public async Task<int> GetSteamLevelAsync(string apiKey, string steamId64)
        {
            try
            {
                var url =
                    $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}";

                var json = await http.GetStringAsync(url).ConfigureAwait(false);
                var root = Serialization.FromJson<GetSteamLevelResponseRoot>(json);

                return root?.Response?.PlayerLevel ?? 0;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"GetSteamLevel failed for '{steamId64}'.");
                return 0;
            }
        }

        public async Task<List<SteamBadge>> GetBadgesAsync(string apiKey, string steamId64)
        {
            try
            {
                var url =
                    $"https://api.steampowered.com/IPlayerService/GetBadges/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}";

                var json = await http.GetStringAsync(url).ConfigureAwait(false);
                var root = Serialization.FromJson<GetBadgesResponseRoot>(json);

                return root?.Response?.Badges ?? new List<SteamBadge>();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"GetBadges failed for '{steamId64}'.");
                return new List<SteamBadge>();
            }
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

                var json = await GetStringCheckedAsync(url, isFriendListRequest: false).ConfigureAwait(false);
                var root = DeserializeOrThrow<GetPlayerSummariesResponseRoot>(json);

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

            var json = await GetStringCheckedAsync(url, isFriendListRequest: false).ConfigureAwait(false);
            var root = DeserializeOrThrow<ResolveVanityUrlResponseRoot>(json);

            if (root?.Response?.Success == 1 &&
                !string.IsNullOrWhiteSpace(root.Response.SteamId))
            {
                return root.Response.SteamId;
            }

            return null;
        }

        public async Task<SteamConnectionTestResult> TestConnectionAsync(string apiKey, string profileInput)
        {
            var normalizedKey = (apiKey ?? string.Empty).Trim();
            var normalizedProfile = (profileInput ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return Result(SteamConnectionTestCode.MissingApiKey);
            }

            if (!Regex.IsMatch(normalizedKey, @"^[A-Fa-f0-9]{32}$"))
            {
                return Result(SteamConnectionTestCode.InvalidApiKeyFormat);
            }

            if (string.IsNullOrWhiteSpace(normalizedProfile))
            {
                return Result(SteamConnectionTestCode.MissingProfile);
            }

            try
            {
                string steamId64;
                string vanity;
                if (!TryParseProfileInput(normalizedProfile, out steamId64, out vanity))
                {
                    return Result(SteamConnectionTestCode.InvalidProfileFormat);
                }

                if (string.IsNullOrWhiteSpace(steamId64))
                {
                    steamId64 = await ResolveVanityUrlAsync(normalizedKey, vanity).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(steamId64))
                    {
                        return Result(SteamConnectionTestCode.ProfileNotFound);
                    }
                }

                // Validate both the API key and the resolved account before testing privacy.
                var summaries = await GetPlayerSummariesAsync(normalizedKey, new[] { steamId64 }).ConfigureAwait(false);
                var self = summaries?.FirstOrDefault(p => string.Equals(p.SteamId, steamId64, StringComparison.Ordinal));
                if (self == null)
                {
                    return Result(SteamConnectionTestCode.ProfileNotFound, steamId64);
                }

                var friends = await GetFriendsAsync(normalizedKey, steamId64).ConfigureAwait(false);
                var friendCount = friends?.Count ?? 0;

                return new SteamConnectionTestResult
                {
                    Code = friendCount > 0
                        ? SteamConnectionTestCode.Success
                        : SteamConnectionTestCode.SuccessNoFriends,
                    SteamId64 = steamId64,
                    PersonaName = self.PersonaName,
                    FriendCount = friendCount
                };
            }
            catch (SteamWebApiException ex)
            {
                logger.Warn(ex, "Steam connection test failed.");
                return Result(MapTestCode(ex.Kind));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unexpected Steam connection test failure.");
                return Result(SteamConnectionTestCode.UnknownError);
            }
        }

        private async Task<string> GetStringCheckedAsync(string url, bool isFriendListRequest)
        {
            HttpResponseMessage response;

            try
            {
                response = await http.GetAsync(url).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                throw new SteamWebApiException(
                    SteamWebApiErrorKind.Timeout,
                    "The Steam Web API request timed out.",
                    innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                throw new SteamWebApiException(
                    SteamWebApiErrorKind.Network,
                    "The Steam Web API could not be reached.",
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
                    throw new SteamWebApiException(
                        SteamWebApiErrorKind.InvalidResponse,
                        "Steam returned an unreadable response.",
                        response.StatusCode,
                        ex);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized && isFriendListRequest)
                {
                    throw new SteamWebApiException(
                        SteamWebApiErrorKind.FriendsListPrivate,
                        "Steam refused access to the friends list. It is probably private.",
                        response.StatusCode);
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new SteamWebApiException(
                        SteamWebApiErrorKind.InvalidApiKey,
                        "Steam rejected the Web API key.",
                        response.StatusCode);
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new SteamWebApiException(
                        SteamWebApiErrorKind.RateLimited,
                        "Steam temporarily rate-limited the requests.",
                        response.StatusCode);
                }

                if ((int)response.StatusCode >= 500)
                {
                    throw new SteamWebApiException(
                        SteamWebApiErrorKind.SteamUnavailable,
                        "The Steam Web API is temporarily unavailable.",
                        response.StatusCode);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new SteamWebApiException(
                        SteamWebApiErrorKind.HttpError,
                        $"Steam returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                        response.StatusCode);
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    throw new SteamWebApiException(
                        SteamWebApiErrorKind.InvalidResponse,
                        "Steam returned an empty response.",
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
            catch (SteamWebApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SteamWebApiException(
                    SteamWebApiErrorKind.InvalidResponse,
                    "Steam returned data in an unexpected format.",
                    innerException: ex);
            }
        }

        private static bool TryParseProfileInput(string input, out string steamId64, out string vanity)
        {
            steamId64 = null;
            vanity = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var value = input.Trim();
            if (Regex.IsMatch(value, @"^\d{17}$"))
            {
                steamId64 = value;
                return true;
            }

            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                var isSteamCommunityHost =
                    string.Equals(uri.Host, "steamcommunity.com", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith(".steamcommunity.com", StringComparison.OrdinalIgnoreCase);

                if (!isSteamCommunityHost)
                {
                    return false;
                }

                var segments = uri.AbsolutePath
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length < 2)
                {
                    return false;
                }

                if (string.Equals(segments[0], "profiles", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Regex.IsMatch(segments[1], @"^\d{17}$"))
                    {
                        return false;
                    }

                    steamId64 = segments[1];
                    return true;
                }

                if (string.Equals(segments[0], "id", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Regex.IsMatch(segments[1], @"^[A-Za-z0-9_-]+$"))
                    {
                        return false;
                    }

                    vanity = segments[1];
                    return true;
                }

                return false;
            }

            if (Regex.IsMatch(value, @"^[A-Za-z0-9_-]+$"))
            {
                vanity = value;
                return true;
            }

            return false;
        }

        private static SteamConnectionTestResult Result(
            SteamConnectionTestCode code,
            string steamId64 = null)
        {
            return new SteamConnectionTestResult
            {
                Code = code,
                SteamId64 = steamId64
            };
        }

        private static SteamConnectionTestCode MapTestCode(SteamWebApiErrorKind kind)
        {
            switch (kind)
            {
                case SteamWebApiErrorKind.InvalidApiKey:
                    return SteamConnectionTestCode.InvalidApiKey;
                case SteamWebApiErrorKind.FriendsListPrivate:
                    return SteamConnectionTestCode.FriendsListPrivate;
                case SteamWebApiErrorKind.RateLimited:
                    return SteamConnectionTestCode.RateLimited;
                case SteamWebApiErrorKind.Timeout:
                    return SteamConnectionTestCode.Timeout;
                case SteamWebApiErrorKind.Network:
                    return SteamConnectionTestCode.Network;
                case SteamWebApiErrorKind.SteamUnavailable:
                    return SteamConnectionTestCode.SteamUnavailable;
                case SteamWebApiErrorKind.InvalidResponse:
                    return SteamConnectionTestCode.InvalidResponse;
                default:
                    return SteamConnectionTestCode.ApiError;
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
