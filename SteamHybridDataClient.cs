using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SteamFriendsFullscreen
{
    public sealed class SteamHybridDataException : Exception
    {
        public bool WebLoginFailed { get; }
        public bool ApiFallbackUnavailable { get; }

        public SteamHybridDataException(
            string message,
            bool webLoginFailed,
            bool apiFallbackUnavailable,
            Exception innerException = null)
            : base(message, innerException)
        {
            WebLoginFailed = webLoginFailed;
            ApiFallbackUnavailable = apiFallbackUnavailable;
        }
    }

    public sealed class SteamHybridDataClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly Func<SteamFriendsFullscreenSettings> settingsProvider;
        private SteamFriendsFullscreenSettings Settings => settingsProvider();
        private readonly SteamWebApiClient apiClient;
        private readonly SteamWebLoginApiClient webLoginApiClient;
        private readonly SteamWebLoginService webLoginService;
        private readonly Action saveSettings;
        private readonly SemaphoreSlim refreshTokenGate = new SemaphoreSlim(1, 1);
        private readonly object webLoginStateGate = new object();
        private DateTime webLoginRetryAfterUtc = DateTime.MinValue;
        private Exception lastWebLoginError;

        public SteamHybridDataClient(
            Func<SteamFriendsFullscreenSettings> settingsProvider,
            SteamWebApiClient apiClient,
            SteamWebLoginApiClient webLoginApiClient,
            SteamWebLoginService webLoginService,
            Action saveSettings)
        {
            this.settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            this.webLoginApiClient = webLoginApiClient ?? throw new ArgumentNullException(nameof(webLoginApiClient));
            this.webLoginService = webLoginService ?? throw new ArgumentNullException(nameof(webLoginService));
            this.saveSettings = saveSettings;
        }

        public void ResetWebLoginBackoff()
        {
            RecordWebLoginSuccess();
        }

        public Task<List<SteamFriend>> GetFriendsAsync(string steamId64)
        {
            return ExecuteAsync(
                token => webLoginApiClient.GetFriendsAsync(token, steamId64),
                key => apiClient.GetFriendsAsync(key, steamId64),
                "friends list");
        }

        public Task<List<string>> GetFriendSteamIdsAsync(string steamId64)
        {
            return ExecuteAsync(
                token => webLoginApiClient.GetFriendSteamIdsAsync(token, steamId64),
                key => apiClient.GetFriendSteamIdsAsync(key, steamId64),
                "friend IDs");
        }

        public Task<List<SteamPlayerSummary>> GetPlayerSummariesAsync(IEnumerable<string> steamIds)
        {
            return ExecuteAsync(
                token => webLoginApiClient.GetPlayerSummariesAsync(token, steamIds),
                key => apiClient.GetPlayerSummariesAsync(key, steamIds),
                "player summaries");
        }

        public Task<List<SteamRecentlyPlayedGame>> GetRecentlyPlayedGamesAsync(string steamId64, int count = 3)
        {
            return ExecuteOptionalAsync(
                token => webLoginApiClient.GetRecentlyPlayedGamesAsync(token, steamId64, count),
                key => apiClient.GetRecentlyPlayedGamesAsync(key, steamId64, count),
                "recent games",
                new List<SteamRecentlyPlayedGame>());
        }

        public Task<List<SteamRecentlyPlayedGame>> GetAllRecentlyPlayedGamesAsync(string steamId64)
        {
            return ExecuteOptionalAsync(
                token => webLoginApiClient.GetAllRecentlyPlayedGamesAsync(token, steamId64),
                key => apiClient.GetAllRecentlyPlayedGamesAsync(key, steamId64),
                "recent games",
                new List<SteamRecentlyPlayedGame>());
        }

        public Task<int> GetSteamLevelAsync(string steamId64)
        {
            return ExecuteOptionalAsync(
                token => webLoginApiClient.GetSteamLevelAsync(token, steamId64),
                key => apiClient.GetSteamLevelAsync(key, steamId64),
                "Steam level",
                0);
        }

        public Task<List<SteamBadge>> GetBadgesAsync(string steamId64)
        {
            return ExecuteOptionalAsync(
                token => webLoginApiClient.GetBadgesAsync(token, steamId64),
                key => apiClient.GetBadgesAsync(key, steamId64),
                "badges",
                new List<SteamBadge>());
        }

        private async Task<T> ExecuteOptionalAsync<T>(
            Func<string, Task<T>> webLoginAction,
            Func<string, Task<T>> apiAction,
            string operationName,
            T defaultValue)
        {
            try
            {
                return await ExecuteAsync(webLoginAction, apiAction, operationName).ConfigureAwait(false);
            }
            catch (SteamHybridDataException ex)
            {
                logger.Warn(ex, "Optional Steam data unavailable for " + operationName + ". Using an empty/default value.");
                return defaultValue;
            }
        }

        private async Task<T> ExecuteAsync<T>(
            Func<string, Task<T>> webLoginAction,
            Func<string, Task<T>> apiAction,
            string operationName)
        {
            Exception webLoginError = null;

            if (Settings.HasWebLoginSession && CanTryWebLogin())
            {
                string attemptedAccessToken = null;
                try
                {
                    attemptedAccessToken = await GetValidAccessTokenAsync(false).ConfigureAwait(false);
                    var result = await webLoginAction(attemptedAccessToken).ConfigureAwait(false);
                    RecordWebLoginSuccess();
                    Settings.MarkWebLoginHealthy();
                    Settings.SetActiveDataSource(SteamActiveDataSource.WebLogin, null);
                    return result;
                }
                catch (SteamWebLoginApiException ex)
                    when (ex.Kind == SteamWebLoginApiErrorKind.Unauthorized)
                {
                    webLoginError = ex;
                    try
                    {
                        var refreshedToken = await GetValidAccessTokenAsync(true, attemptedAccessToken).ConfigureAwait(false);
                        var result = await webLoginAction(refreshedToken).ConfigureAwait(false);
                        RecordWebLoginSuccess();
                        Settings.MarkWebLoginHealthy();
                        Settings.SetActiveDataSource(SteamActiveDataSource.WebLogin, null);
                        return result;
                    }
                    catch (Exception retryException)
                    {
                        webLoginError = retryException;
                    }
                }
                catch (Exception ex)
                {
                    webLoginError = ex;
                }

                RecordWebLoginFailure(webLoginError);
                logger.Warn(webLoginError, "Steam WebLogin failed for " + operationName + ". Trying the API key fallback.");
                Settings.SetWebLoginRuntimeError(webLoginError?.Message);
            }
            else if (Settings.HasWebLoginSession)
            {
                webLoginError = GetLastWebLoginError();
            }

            var apiKey = Settings.SteamApiKey?.Trim();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    var result = await apiAction(apiKey).ConfigureAwait(false);
                    Settings.SetActiveDataSource(
                        SteamActiveDataSource.ApiKeyFallback,
                        webLoginError == null ? null : webLoginError.Message);
                    return result;
                }
                catch (Exception apiException)
                {
                    throw new SteamHybridDataException(
                        "Steam WebLogin failed and the API key fallback also failed.",
                        webLoginError != null,
                        false,
                        apiException);
                }
            }

            Settings.SetActiveDataSource(SteamActiveDataSource.None, webLoginError?.Message);

            if (!Settings.HasWebLoginSession)
            {
                throw new SteamHybridDataException(
                    "Connect Steam with WebLogin or configure an optional Steam Web API key.",
                    false,
                    true);
            }

            throw new SteamHybridDataException(
                "The Steam WebLogin session is unavailable and no API key fallback is configured. Reconnect Steam.",
                true,
                true,
                webLoginError);
        }

        private bool CanTryWebLogin()
        {
            lock (webLoginStateGate)
            {
                return DateTime.UtcNow >= webLoginRetryAfterUtc;
            }
        }

        private void RecordWebLoginSuccess()
        {
            lock (webLoginStateGate)
            {
                webLoginRetryAfterUtc = DateTime.MinValue;
                lastWebLoginError = null;
            }
        }

        private void RecordWebLoginFailure(Exception exception)
        {
            lock (webLoginStateGate)
            {
                webLoginRetryAfterUtc = DateTime.UtcNow.AddMinutes(1);
                lastWebLoginError = exception;
            }
        }

        private Exception GetLastWebLoginError()
        {
            lock (webLoginStateGate)
            {
                return lastWebLoginError;
            }
        }

        private async Task<string> GetValidAccessTokenAsync(
            bool forceRefresh,
            string rejectedAccessToken = null)
        {
            var accessToken = Settings.SteamWebLoginAccessToken;
            if (!forceRefresh &&
                !string.IsNullOrWhiteSpace(accessToken) &&
                Settings.SteamWebLoginAccessTokenExpiresUtc > DateTime.UtcNow.AddMinutes(2))
            {
                return accessToken;
            }

            await refreshTokenGate.WaitAsync().ConfigureAwait(false);
            try
            {
                accessToken = Settings.SteamWebLoginAccessToken;
                if (!forceRefresh &&
                    !string.IsNullOrWhiteSpace(accessToken) &&
                    Settings.SteamWebLoginAccessTokenExpiresUtc > DateTime.UtcNow.AddMinutes(2))
                {
                    return accessToken;
                }

                // Another request may already have refreshed the rejected token while
                // this request was waiting for the gate. Reuse the newer token instead
                // of generating several access tokens in succession.
                if (forceRefresh &&
                    !string.IsNullOrWhiteSpace(accessToken) &&
                    !string.IsNullOrWhiteSpace(rejectedAccessToken) &&
                    !string.Equals(accessToken, rejectedAccessToken, StringComparison.Ordinal))
                {
                    return accessToken;
                }

                Settings.SetWebLoginRefreshing();
                try
                {
                    var session = await webLoginService.RefreshSessionAsync(
                        Settings.SteamWebLoginSteamId64,
                        Settings.SteamWebLoginAccountName,
                        Settings.SteamWebLoginRefreshToken,
                        CancellationToken.None).ConfigureAwait(false);

                    Settings.ApplyWebLoginSession(session);
                    RecordWebLoginSuccess();
                    saveSettings?.Invoke();
                    return session.AccessToken;
                }
                catch (Exception ex)
                {
                    RecordWebLoginFailure(ex);
                    throw;
                }
            }
            finally
            {
                refreshTokenGate.Release();
            }
        }
    }
}
