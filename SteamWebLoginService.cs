using Playnite.SDK;
using QRCoder;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SteamFriendsFullscreen
{
    public sealed class SteamWebLoginService : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource activeOperation;
        private bool disposed;

        public event Action<byte[]> QrCodeChanged;

        public void CancelCurrentOperation()
        {
            try { activeOperation?.Cancel(); } catch { }
        }

        public async Task<SteamWebLoginSession> SignInWithQrAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                CancelCurrentOperation();
                activeOperation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = activeOperation.Token;

                return await WithConnectedClientAsync(async steamClient =>
                {
                    var details = new AuthSessionDetails
                    {
                        DeviceFriendlyName = "Steam Friends Fullscreen",
                        PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_WebBrowser,
                        WebsiteID = "Community",
                        IsPersistentSession = true
                    };

                    var authSession = await steamClient.Authentication
                        .BeginAuthSessionViaQRAsync(details)
                        .ConfigureAwait(false);

                    Action publishQr = () => PublishQr(authSession.ChallengeURL);
                    authSession.ChallengeURLChanged = publishQr;
                    publishQr();

                    var pollResponse = await authSession
                        .PollingWaitForResultAsync(token)
                        .ConfigureAwait(false);

                    var steamId64 = SteamJwt.TryGetSteamId64(pollResponse.AccessToken);
                    if (string.IsNullOrWhiteSpace(steamId64))
                    {
                        steamId64 = SteamJwt.TryGetSteamId64(pollResponse.RefreshToken);
                    }

                    if (string.IsNullOrWhiteSpace(steamId64))
                    {
                        throw new InvalidOperationException("Steam authentication succeeded, but the SteamID could not be read from the token.");
                    }

                    return new SteamWebLoginSession
                    {
                        AccountName = pollResponse.AccountName,
                        SteamId64 = steamId64,
                        AccessToken = pollResponse.AccessToken,
                        RefreshToken = pollResponse.RefreshToken,
                        AccessTokenExpiresUtc = SteamJwt.GetExpirationUtc(pollResponse.AccessToken)
                    };
                }, token).ConfigureAwait(false);
            }
            finally
            {
                try { activeOperation?.Dispose(); } catch { }
                activeOperation = null;
                operationGate.Release();
            }
        }

        public async Task<SteamWebLoginSession> RefreshSessionAsync(
            string steamId64,
            string accountName,
            string refreshToken,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(steamId64) ||
                string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new InvalidOperationException("The saved Steam WebLogin session is incomplete.");
            }

            await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                CancelCurrentOperation();
                activeOperation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = activeOperation.Token;

                return await WithConnectedClientAsync(async steamClient =>
                {
                    ulong steamIdValue;
                    if (!ulong.TryParse(steamId64, out steamIdValue))
                    {
                        throw new InvalidOperationException("The saved SteamID64 is invalid.");
                    }

                    var result = await steamClient.Authentication
                        .GenerateAccessTokenForAppAsync(new SteamID(steamIdValue), refreshToken, true)
                        .ConfigureAwait(false);

                    var newRefreshToken = string.IsNullOrWhiteSpace(result.RefreshToken)
                        ? refreshToken
                        : result.RefreshToken;

                    return new SteamWebLoginSession
                    {
                        AccountName = accountName,
                        SteamId64 = steamId64,
                        AccessToken = result.AccessToken,
                        RefreshToken = newRefreshToken,
                        AccessTokenExpiresUtc = SteamJwt.GetExpirationUtc(result.AccessToken)
                    };
                }, token).ConfigureAwait(false);
            }
            finally
            {
                try { activeOperation?.Dispose(); } catch { }
                activeOperation = null;
                operationGate.Release();
            }
        }

        private async Task<T> WithConnectedClientAsync<T>(
            Func<SteamClient, Task<T>> action,
            CancellationToken cancellationToken)
        {
            var steamClient = new SteamClient();
            var manager = new CallbackManager(steamClient);
            var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            manager.Subscribe<SteamClient.ConnectedCallback>(_ => connected.TrySetResult(true));
            manager.Subscribe<SteamClient.DisconnectedCallback>(_ => disconnected.TrySetResult(true));

            using (var callbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var callbackLoop = Task.Run(() =>
                {
                    while (!callbackCts.IsCancellationRequested)
                    {
                        try
                        {
                            manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(250));
                        }
                        catch (Exception ex)
                        {
                            if (!callbackCts.IsCancellationRequested)
                            {
                                logger.Warn(ex, "SteamKit callback loop failed.");
                            }
                        }
                    }
                }, callbackCts.Token);

                try
                {
                    steamClient.Connect();
                    await WaitWithTimeoutAsync(connected.Task, TimeSpan.FromSeconds(20), cancellationToken)
                        .ConfigureAwait(false);

                    return await action(steamClient).ConfigureAwait(false);
                }
                finally
                {
                    try { steamClient.Disconnect(); } catch { }
                    callbackCts.Cancel();
                    try { await callbackLoop.ConfigureAwait(false); } catch { }
                }
            }
        }

        private static async Task WaitWithTimeoutAsync(
            Task task,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delay = Task.Delay(timeout, timeoutCts.Token);
                var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
                if (completed != task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("Steam did not accept the WebLogin connection in time.");
                }

                timeoutCts.Cancel();
                await task.ConfigureAwait(false);
            }
        }

        private void PublishQr(string challengeUrl)
        {
            if (string.IsNullOrWhiteSpace(challengeUrl))
            {
                return;
            }

            try
            {
                byte[] pngBytes;
                using (var generator = new QRCodeGenerator())
                using (var data = generator.CreateQrCode(challengeUrl, QRCodeGenerator.ECCLevel.L))
                using (var code = new PngByteQRCode(data))
                {
                    pngBytes = code.GetGraphic(10, true);
                }

                QrCodeChanged?.Invoke(pngBytes);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to generate the Steam WebLogin QR code.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SteamWebLoginService));
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            CancelCurrentOperation();
            try { activeOperation?.Dispose(); } catch { }
            activeOperation = null;
            // Do not dispose operationGate here: an in-flight operation may still release it during shutdown.
        }
    }

    internal static class SteamJwt
    {
        public static string TryGetSteamId64(string token)
        {
            var payload = DecodePayload(token);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            var match = Regex.Match(payload, "\\\"sub\\\"\\s*:\\s*\\\"?(?<id>765[0-9]{14})\\\"?", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(payload, "\\\"steamid\\\"\\s*:\\s*\\\"?(?<id>765[0-9]{14})\\\"?", RegexOptions.IgnoreCase);
            }

            return match.Success ? match.Groups["id"].Value : null;
        }

        public static DateTime GetExpirationUtc(string token)
        {
            var payload = DecodePayload(token);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                var match = Regex.Match(payload, "\\\"exp\\\"\\s*:\\s*(?<exp>[0-9]+)", RegexOptions.IgnoreCase);
                long seconds;
                if (match.Success && long.TryParse(match.Groups["exp"].Value, out seconds))
                {
                    try
                    {
                        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
                    }
                    catch { }
                }
            }

            // Access tokens are short-lived. Refresh slightly early when no expiry can be parsed.
            return DateTime.UtcNow.AddMinutes(15);
        }

        private static string DecodePayload(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return null;
                }

                var parts = token.Split('.');
                if (parts.Length < 2)
                {
                    return null;
                }

                var value = parts[1].Replace('-', '+').Replace('_', '/');
                switch (value.Length % 4)
                {
                    case 2: value += "=="; break;
                    case 3: value += "="; break;
                }

                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return null;
            }
        }
    }
}
