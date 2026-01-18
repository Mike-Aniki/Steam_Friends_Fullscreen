using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SteamFriendsFullscreen
{
    public class SteamFriendsFullscreen : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SteamFriendsFullscreenSettingsViewModel settingsVm;

        // Root exposed to the theme (via AddSettingsSupport)
        public SteamFriendsFullscreenSettings Settings => settingsVm?.Settings;

        private DispatcherTimer refreshTimer;
        private bool isRefreshing = false;
        private DateTime lastSuccessUtc = DateTime.MinValue;

        private SteamWebApiClient steamClient;
        private WindowsToastService windowsToasts;

        private const int FixedRefreshSeconds = 60;
        private const int FixedMaxFriendsShown = 15;
        private const int FixedMaxOfflineShown = 40;

        // Pause when launching a game 
        private DateTime pausedUntilUtc = DateTime.MinValue;
        private static readonly TimeSpan PauseOnGameStart = TimeSpan.FromMinutes(10);

        // Anti-spam API : cache friendIds 
        private System.Collections.Generic.List<string> cachedFriendIds = null;
        private DateTime friendIdsLastFetchUtc = DateTime.MinValue;
        private readonly TimeSpan friendIdsCacheTtl = TimeSpan.FromHours(6);

        //  Cache SteamId64 résolu (URL/vanity) 
        private string cachedResolvedSteamId64 = null;
        private string cachedSteamIdInput = null;
        private DateTime steamIdResolveLastUtc = DateTime.MinValue;
        private readonly TimeSpan steamIdResolveTtl = TimeSpan.FromHours(24);

        // ===== Notifications: state tracking =====
        private readonly Dictionary<string, string> lastState = new Dictionary<string, string>();
        private readonly Dictionary<string, string> lastGame = new Dictionary<string, string>();
        private readonly Dictionary<string, DateTime> lastToastUtc = new Dictionary<string, DateTime>();

        private bool hasBaseline = false;

        private CancellationTokenSource toastCts;
        private static readonly TimeSpan ToastCooldown = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(6);


        // Anti-stutter UI
        private string lastUiSignature = null;
        private string lastSelfState = null;

        // Cache avatars local
        private string avatarCacheDir;

        private readonly HttpClient http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly SemaphoreSlim avatarDlSem = new SemaphoreSlim(2, 2);
        private readonly ConcurrentDictionary<string, byte> avatarDlInProgress = new ConcurrentDictionary<string, byte>();

        // Avatar download limit per refresh
        private const int MaxAvatarDownloadsPerRefresh = 4;

        // Cleaning avatar cache
        private DateTime lastAvatarCleanupUtc = DateTime.MinValue;
        private readonly TimeSpan avatarCleanupInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan avatarMaxAge = TimeSpan.FromDays(30);

        public override Guid Id { get; } = Guid.Parse("d8d67c7e-a797-45d7-ae44-3d53ddecc8d1");

        public SteamFriendsFullscreen(IPlayniteAPI api) : base(api)
        {
            settingsVm = new SteamFriendsFullscreenSettingsViewModel(this);

            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            steamClient = new SteamWebApiClient();
            windowsToasts = new WindowsToastService();
            windowsToasts.EnsureInitialized();

            avatarCacheDir = Path.Combine(GetPluginUserDataPath(), "AvatarCache");
            Directory.CreateDirectory(avatarCacheDir);

            AddSettingsSupportSafe("SteamFriendsFullscreen", "Settings");

            StartTimer();
        }


        private void AddSettingsSupportSafe(string sourceName, string settingsRootPropertyName)
        {
            try
            {
                AddSettingsSupport(new AddSettingsSupportArgs
                {
                    SourceName = sourceName,
                    SettingsRoot = settingsRootPropertyName
                });
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "AddSettingsSupport indisponible sur cette version/contexte.");
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            StartTimer();
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            StopTimer();
            try { http.Dispose(); } catch { }
        }

        private bool IsFullscreenMode()
        {
            try
            {
                var mode = PlayniteApi?.ApplicationInfo?.Mode;
                return mode != null && mode.ToString().Equals("Fullscreen", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }


        private void StartTimer()
        {
            // Fullscreen only
            if (!IsFullscreenMode())
            {
                StopTimer();
                return;
            }

            var interval = TimeSpan.FromSeconds(FixedRefreshSeconds);


            if (refreshTimer != null)
            {
                refreshTimer.Interval = interval;
                if (!refreshTimer.IsEnabled)
                {
                    refreshTimer.Start();
                }

                _ = RefreshSteamPresenceAsync();
                return;
            }

            refreshTimer = new DispatcherTimer { Interval = interval };
            refreshTimer.Tick += (s, e) => { _ = RefreshSteamPresenceAsync(); };
            refreshTimer.Start();

            _ = RefreshSteamPresenceAsync();
        }

        private void StopTimer()
        {
            if (refreshTimer == null)
            {
                return;
            }

            try
            {
                refreshTimer.Stop();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to stop refresh timer.");
            }
            finally
            {
                refreshTimer = null;
            }
        }

        private void ShowToast(string message, string avatar)
        {
            toastCts?.Cancel();
            toastCts = new CancellationTokenSource();
            var ct = toastCts.Token;

            InvokeOnUi(() =>
            {
                Settings.ToastMessage = message;
                Settings.ToastAvatar = avatar;
                Settings.ToastToken = DateTime.UtcNow.Ticks;
                Settings.ToastFlip = !Settings.ToastFlip;
                Settings.ToastIsVisible = true;

            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ToastDuration, ct).ConfigureAwait(false);
                    InvokeOnUi(() => Settings.ToastIsVisible = false);
                }
                catch
                {
                    // cancelled
                }
            });
        }

        public void DebugTriggerTestNotification()
        {
            try
            {
                if (Settings == null)
                {
                    return;
                }

                var mode = Settings.NotificationOutputMode;

                // Respect the same logic as production
                if (mode == NotificationOutputMode.Off)
                {
                    return;
                }

                var sendPlaynite = (mode == NotificationOutputMode.PlayniteOnly || mode == NotificationOutputMode.PlayniteAndWindows);
                var sendWindows = (mode == NotificationOutputMode.WindowsOnly || mode == NotificationOutputMode.PlayniteAndWindows);

                // Respect toggles: if both off -> no notification
                if (!Settings.NotifyOnConnect && !Settings.NotifyOnGameStart)
                {
                    return;
                }

                // Build a localized test message using the same keys as production
                var friendName = "FriendName";
                var stateLoc = LocalizeState("online");

                var tpl = GetStringSafe("LOCSteamFriendsToast_Online", "{0} is now {1}");
                var msg = string.Format(tpl, friendName, stateLoc);

                var title = friendName;

                if (sendPlaynite)
                {
                    ShowToast(msg, null);
                }

                if (sendWindows)
                {
                    windowsToasts?.Show(title, msg, null);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Debug test notification failed.");
            }
        }



        private void DetectAndQueueToast(List<FriendPresenceDto> dtos, string selfSteamId64)
        {
            if (dtos == null || dtos.Count == 0)
            {
                return;
            }

            var mode = Settings.NotificationOutputMode;

            // Off => baseline only
            if (mode == NotificationOutputMode.Off)
            {
                UpdateBaseline(dtos, selfSteamId64);
                hasBaseline = true;
                return;
            }

            var sendPlaynite = (mode == NotificationOutputMode.PlayniteOnly || mode == NotificationOutputMode.PlayniteAndWindows);
            var sendWindows = (mode == NotificationOutputMode.WindowsOnly || mode == NotificationOutputMode.PlayniteAndWindows);

            // If user disabled both toggles -> no notifications
            var wantGameStart = Settings.NotifyOnGameStart;
            var wantConnect = Settings.NotifyOnConnect;

            if (!wantGameStart && !wantConnect)
            {
                UpdateBaseline(dtos, selfSteamId64);
                hasBaseline = true;
                return;
            }

            // First successful refresh: baseline only, no notification spam
            if (!hasBaseline)
            {
                UpdateBaseline(dtos, selfSteamId64);
                hasBaseline = true;
                return;
            }

            var now = DateTime.UtcNow;

            for (int i = 0; i < dtos.Count; i++)
            {
                var f = dtos[i];
                if (f == null || string.IsNullOrWhiteSpace(f.steamid))
                {
                    continue;
                }

                // Self safety (normally not present in dtos anyway)
                if (!string.IsNullOrWhiteSpace(selfSteamId64) && f.steamid == selfSteamId64)
                {
                    continue;
                }

                var newState = f.state ?? "offline";
                var newGame = string.IsNullOrWhiteSpace(f.game) ? null : f.game;

                lastState.TryGetValue(f.steamid, out var oldState);
                lastGame.TryGetValue(f.steamid, out var oldGame);

                oldState = oldState ?? "offline";

                bool shouldToast = false;
                string message = null;

                // 1) Notify on connect
                if (wantConnect)
                {
                    if (oldState == "offline" && newState != "offline")
                    {
                        shouldToast = true;
                        var tpl = GetStringSafe("LOCSteamFriendsToast_Online", "{0} is now {1}");
                        message = string.Format(tpl, f.name ?? "Friend", f.stateLoc ?? newState);
                    }
                }

                // 2) Notify on game start (only if we didn't already toast)
                if (!shouldToast && wantGameStart)
                {
                    var becameInGame = (oldState != "ingame" && newState == "ingame");
                    var startedGame = (string.IsNullOrWhiteSpace(oldGame) && !string.IsNullOrWhiteSpace(newGame));

                    if (becameInGame || startedGame)
                    {
                        shouldToast = true;
                        if (!string.IsNullOrWhiteSpace(newGame))
                        {
                            var tpl = GetStringSafe("LOCSteamFriendsToast_GameStart", "{0} started playing {1}");
                            message = string.Format(tpl, f.name ?? "Friend", newGame);
                        }
                        else
                        {
                            // fallback if Steam doesn't return the game name
                            var tpl = GetStringSafe("LOCSteamFriendsToast_GameStart", "{0} started playing {1}");
                            message = string.Format(tpl, f.name ?? "Friend", GetStringSafe("LOCSteamInGame", "In game"));
                        }

                    }
                }

                if (shouldToast)
                {
                    // Anti-spam cooldown per friend
                    if (lastToastUtc.TryGetValue(f.steamid, out var last) && (now - last) < ToastCooldown)
                    {
                        lastState[f.steamid] = newState;
                        lastGame[f.steamid] = newGame;
                        continue;
                    }

                    lastToastUtc[f.steamid] = now;
                    if (sendPlaynite)
                    {
                        ShowToast(message, f.avatar);
                    }

                    if (sendWindows)
                    {
                        // Titre court + message : Windows aime la sobriété
                        var title = f.name ?? "Friend";
                        windowsToasts.Show(title, message, f.avatar);
                    }

                    lastState[f.steamid] = newState;
                    lastGame[f.steamid] = newGame;
                    break;

                }

                // Update snapshot
                lastState[f.steamid] = newState;
                lastGame[f.steamid] = newGame;
            }
        }

        private void UpdateBaseline(List<FriendPresenceDto> dtos, string selfSteamId64)
        {
            for (int i = 0; i < dtos.Count; i++)
            {
                var f = dtos[i];
                if (f == null || string.IsNullOrWhiteSpace(f.steamid))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(selfSteamId64) && f.steamid == selfSteamId64)
                {
                    continue;
                }

                lastState[f.steamid] = f.state ?? "offline";
                lastGame[f.steamid] = string.IsNullOrWhiteSpace(f.game) ? null : f.game;
            }
        }


        private void InvokeOnUi(Action action)
        {
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp == null || disp.CheckAccess())
                {
                    action();
                }
                else
                {
                    disp.BeginInvoke(action);
                }
            }
            catch
            {
                action();
            }
        }

        private async Task<string> ResolveSteamId64Async(string apiKey, string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return null;
            }

            var input = userInput.Trim();

            // 1) ID64 (17 chiffres)
            if (input.Length == 17 && input.All(char.IsDigit))
            {
                return input;
            }


            var nowUtc = DateTime.UtcNow;
            if (cachedSteamIdInput == input &&
                !string.IsNullOrWhiteSpace(cachedResolvedSteamId64) &&
                (nowUtc - steamIdResolveLastUtc) < steamIdResolveTtl)
            {
                return cachedResolvedSteamId64;
            }

            // 2) URL profiles
            var profilesMarker = "/profiles/";
            var idxProfiles = input.IndexOf(profilesMarker, StringComparison.OrdinalIgnoreCase);
            if (idxProfiles >= 0)
            {
                var after = input.Substring(idxProfiles + profilesMarker.Length);
                var digits = new string(after.TakeWhile(char.IsDigit).ToArray());
                if (!string.IsNullOrWhiteSpace(digits))
                {
                    cachedSteamIdInput = input;
                    cachedResolvedSteamId64 = digits;
                    steamIdResolveLastUtc = nowUtc;
                    return digits;
                }
            }

            // 3) URL id/<vanity>
            var idMarker = "/id/";
            var idxId = input.IndexOf(idMarker, StringComparison.OrdinalIgnoreCase);
            string vanity = null;

            if (idxId >= 0)
            {
                var after = input.Substring(idxId + idMarker.Length);
                vanity = new string(after.TakeWhile(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            }
            else
            {
                // 4) just a nickname
                vanity = input;
            }

            if (string.IsNullOrWhiteSpace(vanity))
            {
                return null;
            }

            // Web API : ResolveVanityURL
            try
            {
                var resolved = await steamClient.ResolveVanityUrlAsync(apiKey, vanity).ConfigureAwait(false);

                cachedSteamIdInput = input;
                cachedResolvedSteamId64 = resolved;
                steamIdResolveLastUtc = nowUtc;

                return resolved;
            }
            catch
            {
                return null;
            }
        }


        // Main refresh
        private async Task RefreshSteamPresenceAsync()
        {
            if (isRefreshing)
            {
                return;
            }
            // Fullscreen only
            if (!IsFullscreenMode())
            {
                return;
            }

            // Pause after launching a game
            if (DateTime.UtcNow < pausedUntilUtc)
            {
                return;
            }

            if (Settings == null)
            {
                return;
            }

            var apiKey = Settings.SteamApiKey?.Trim();
            var steamIdInput = Settings.SteamId64?.Trim();
            var steamId64 = await ResolveSteamId64Async(apiKey, steamIdInput).ConfigureAwait(false);

            // Not configured > reset
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(steamId64))
            {
                InvokeOnUi(() =>
                {
                    Settings.OnlineCount = 0;
                    Settings.InGameCount = 0;
                    Settings.OfflineCount = 0;
                    Settings.Friends.Clear();

                    Settings.LastError = "Missing API key or Steam profile.";
                    Settings.LastUpdateUtc = DateTime.MinValue;


                });

                lastUiSignature = null;
                lastSelfState = null;
                return;
            }

            isRefreshing = true;

            try
            {
                var nowUtc = DateTime.UtcNow;

                // Avatar cache cleanup (once every 24 hours)
                MaybeCleanupAvatarCache(nowUtc);

                // 1) Cache friendIds (anti-spam)
                var shouldRefreshFriendIds =
                    cachedFriendIds == null ||
                    cachedFriendIds.Count == 0 ||
                    (nowUtc - friendIdsLastFetchUtc) > friendIdsCacheTtl;

                if (shouldRefreshFriendIds)
                {
                    var ids = await steamClient.GetFriendSteamIdsAsync(apiKey, steamId64).ConfigureAwait(false);
                    cachedFriendIds = ids != null ? ids.ToList() : new System.Collections.Generic.List<string>();
                    friendIdsLastFetchUtc = nowUtc;
                }

                // No friends
                if (cachedFriendIds == null || cachedFriendIds.Count == 0)
                {
                    InvokeOnUi(() =>
                    {
                        Settings.OnlineCount = 0;
                        Settings.InGameCount = 0;
                        Settings.OfflineCount = 0;
                        Settings.Friends.Clear();

                        Settings.LastError = "No friends returned (Steam API).";
                        Settings.LastUpdateUtc = DateTime.MinValue;
                    });

                    lastUiSignature = "empty";
                    lastSuccessUtc = nowUtc;
                    return;
                }


                // 2) Presence only (1 call per refresh) + include SELF
                var idsForSummaries = cachedFriendIds.ToList();
                if (!idsForSummaries.Contains(steamId64))
                {
                    idsForSummaries.Add(steamId64);
                }

                var players = await steamClient.GetPlayerSummariesAsync(apiKey, idsForSummaries).ConfigureAwait(false);

                // Self + friends split
                var self = players?.FirstOrDefault(p => p.SteamId == steamId64);
                var friendPlayers = players?.Where(p => p.SteamId != steamId64).ToList()
                                  ?? new System.Collections.Generic.List<SteamPlayerSummary>();


                // 3) DTO + avatar cache (limited)
                int avatarDownloadsScheduled = 0;

                var dtos = friendPlayers.Select(p =>
                {
                    var rawState = MapState(p);

                    var dto = new FriendPresenceDto
                    {
                        name = p.PersonaName,
                        state = rawState,
                        stateLoc = LocalizeState(rawState),
                        game = string.IsNullOrWhiteSpace(p.GameExtraInfo) ? null : p.GameExtraInfo,
                        steamid = p.SteamId
                    };


                    // Avatar: promotes local cache
                    var localPath = GetAvatarFilePath(dto.steamid);
                    if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                    {
                        dto.avatar = ToFileUri(localPath);
                    }
                    else
                    {
                        // Fallback remote
                        dto.avatar = p.AvatarFull;

                        if (avatarDownloadsScheduled < MaxAvatarDownloadsPerRefresh)
                        {
                            avatarDownloadsScheduled++;
                            _ = CacheAvatarAsync(dto.steamid, p.AvatarFull);
                        }
                    }

                    return dto;
                }).ToList();

                DetectAndQueueToast(dtos, steamId64);

                // ===== Update SELF info =====
                if (self != null)
                {
                    var myState = MapState(self);

                    // Avatar local cache
                    string myAvatar = null;
                    var myLocalPath = GetAvatarFilePath(steamId64);

                    if (!string.IsNullOrWhiteSpace(myLocalPath) && File.Exists(myLocalPath))
                    {
                        myAvatar = ToFileUri(myLocalPath);
                    }
                    else
                    {
                        myAvatar = self.AvatarFull;
                        _ = CacheAvatarAsync(steamId64, self.AvatarFull);
                    }

                    // ✅ Update stateLoc ONLY if state changed
                    var stateChanged = !string.Equals(lastSelfState, myState, StringComparison.Ordinal);
                    if (stateChanged)
                    {
                        lastSelfState = myState;
                    }

                    InvokeOnUi(() =>
                    {
                        Settings.SelfName = self.PersonaName;
                        Settings.SelfState = myState;
                        Settings.SelfStateLoc = LocalizeState(myState);
                        Settings.SelfGame = string.IsNullOrWhiteSpace(self.GameExtraInfo) ? null : self.GameExtraInfo;
                        Settings.SelfAvatar = myAvatar;
                    });
                }
                else
                {
                    lastSelfState = "offline";

                    InvokeOnUi(() =>
                    {
                        Settings.SelfName = null;
                        Settings.SelfState = "offline";
                        Settings.SelfStateLoc = LocalizeState("offline");
                        Settings.SelfGame = null;
                        Settings.SelfAvatar = null;
                    });
                }



                // 4) Global counters
                var onlineCount = dtos.Count(d => d.state != "offline");
                var inGameCount = dtos.Count(d => d.state == "ingame");
                var offlineCount = dtos.Count(d => d.state == "offline");

                // 5) ONLINE + tri + limite
                var onlineSorted = dtos
                    .Where(d => d.state != "offline")
                    .OrderBy(d => Rank(d.state))
                    .ThenBy(d => d.name)
                    .ToList();

                var maxOnline = FixedMaxFriendsShown;
                var onlineTop = onlineSorted.Take(maxOnline).ToList();

                // 6) OFFLINE (optional): max 40 + no avatar download (performance)
                var offlineList = new System.Collections.Generic.List<FriendPresenceDto>();
                if (Settings.ShowOffline && FixedMaxOfflineShown > 0)
                {
                    offlineList = dtos
                        .Where(d => d.state == "offline")
                        .OrderBy(d => d.name)
                        .Take(FixedMaxOfflineShown) // cap fixed (40)
                        .ToList();

                    // Offline: avatar only if already in local cache, otherwise null
                    for (int i = 0; i < offlineList.Count; i++)
                    {
                        var f = offlineList[i];
                        var localPath = GetAvatarFilePath(f.steamid);
                        if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                        {
                            f.avatar = ToFileUri(localPath);
                        }
                        else
                        {
                            f.avatar = null;
                        }
                    }
                }

                // 7) Signature: if identical -> no UI update
                var signature = BuildUiSignature(onlineCount, inGameCount, offlineCount, onlineTop, offlineList);

                if (signature == lastUiSignature)
                {
                    InvokeOnUi(() =>
                    {
                        Settings.LastError = null;
                        Settings.LastUpdateUtc = nowUtc;
                    });

                    lastSuccessUtc = nowUtc;
                    return;
                }





                lastUiSignature = signature;

                // 8) Update UI 
                InvokeOnUi(() =>
                {
                    Settings.OnlineCount = onlineCount;
                    Settings.InGameCount = inGameCount;
                    Settings.OfflineCount = offlineCount;

                    Settings.Friends.Clear();

                    // Online first
                    for (int i = 0; i < onlineTop.Count; i++)
                    {
                        Settings.Friends.Add(onlineTop[i]);
                    }

                    // Offline after
                    if (offlineList != null && offlineList.Count > 0)
                    {
                        for (int i = 0; i < offlineList.Count; i++)
                        {
                            Settings.Friends.Add(offlineList[i]);
                        }
                    }
                    Settings.LastError = null;
                    Settings.LastUpdateUtc = nowUtc;

                });

                lastSuccessUtc = nowUtc;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to refresh Steam friends presence.");

                if (DateTime.UtcNow - lastSuccessUtc > TimeSpan.FromMinutes(10))
                {
                    InvokeOnUi(() =>
                    {
                        Settings.OnlineCount = 0;
                        Settings.InGameCount = 0;
                        Settings.OfflineCount = 0;
                        Settings.Friends.Clear();

                        Settings.LastUpdateUtc = DateTime.MinValue;
                        Settings.LastError = "Steam API error (no successful refresh for 10 minutes).";
                    });

                    lastUiSignature = null;
                }
            }
            finally
            {
                isRefreshing = false;
            }
        }

        // ===== Signature stable (anti-stutter) =====
        private string BuildUiSignature(
            int online, int ingame, int offline,
            System.Collections.Generic.List<FriendPresenceDto> onlineTop,
            System.Collections.Generic.List<FriendPresenceDto> offlineList)
        {
            var sb = new StringBuilder(512);
            sb.Append("o=").Append(online).Append("|g=").Append(ingame).Append("|f=").Append(offline).Append("|");

            if (onlineTop != null)
            {
                for (int i = 0; i < onlineTop.Count; i++)
                {
                    var f = onlineTop[i];
                    sb.Append(f.steamid ?? "").Append("|");
                    sb.Append(f.state ?? "").Append("|");
                    sb.Append(f.stateLoc ?? "").Append("|"); 
                    sb.Append(f.game ?? "").Append("|");
                    sb.Append(f.avatar ?? "").Append("|"); 
                }
            }

            sb.Append("#");

            if (offlineList != null)
            {
                for (int i = 0; i < offlineList.Count; i++)
                {
                    var f = offlineList[i];
                    sb.Append(f.steamid ?? "").Append("|");
                    sb.Append(f.avatar ?? "").Append("|");
                    sb.Append(f.stateLoc ?? "").Append("|");

                }
            }

            return sb.ToString();
        }

        //  Avatar cache 
        private string GetAvatarFilePath(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                return null;
            }

            return Path.Combine(avatarCacheDir, steamId + ".jpg");
        }

        private string ToFileUri(string path)
        {
            try
            {
                return new Uri(path).AbsoluteUri;
            }
            catch
            {
                return path;
            }
        }

        private async Task CacheAvatarAsync(string steamId, string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(steamId) || string.IsNullOrWhiteSpace(avatarUrl))
            {
                return;
            }

            var path = GetAvatarFilePath(steamId);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (File.Exists(path))
            {
                return;
            }

            if (!avatarDlInProgress.TryAdd(steamId, 0))
            {
                return;
            }

            try
            {
                await avatarDlSem.WaitAsync().ConfigureAwait(false);

                if (File.Exists(path))
                {
                    return;
                }

                var data = await http.GetByteArrayAsync(avatarUrl).ConfigureAwait(false);

                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, data);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tmp, path);
            }
            catch (Exception)
            {
                logger.Warn($"Avatar cache download failed for {steamId}");
                try
                {
                    var tmp = path + ".tmp";
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch { }
            }
            finally
            {
                avatarDlInProgress.TryRemove(steamId, out _);
                try { avatarDlSem.Release(); } catch { }
            }
        }

        // Nettoyage cache avatars 
        private void MaybeCleanupAvatarCache(DateTime nowUtc)
        {
            if (nowUtc - lastAvatarCleanupUtc < avatarCleanupInterval)
            {
                return;
            }

            lastAvatarCleanupUtc = nowUtc;

            Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(avatarCacheDir))
                    {
                        return;
                    }

                    var files = Directory.GetFiles(avatarCacheDir, "*.jpg");
                    for (int i = 0; i < files.Length; i++)
                    {
                        var f = files[i];

                        DateTime lastWriteUtc;
                        try
                        {
                            lastWriteUtc = File.GetLastWriteTimeUtc(f);
                        }
                        catch
                        {
                            continue;
                        }

                        if ((nowUtc - lastWriteUtc) > avatarMaxAge)
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Avatar cache cleanup failed.");
                }
            });
        }

        // Helpers
        private string MapState(SteamPlayerSummary p)
        {
            if (p == null)
            {
                return "offline";
            }

            if (!string.IsNullOrWhiteSpace(p.GameExtraInfo))
            {
                return "ingame";
            }

            switch (p.PersonaState)
            {
                case 0: return "offline";
                case 1: return "online";
                case 2: return "busy";
                case 3: return "away";
                case 4: return "snooze";
                case 5: return "online";
                case 6: return "online";
                default: return "offline";
            }
        }

        private int Rank(string state)
        {
            switch (state)
            {
                case "ingame": return 0;
                case "online": return 1;
                case "away": return 2;
                case "busy": return 3;
                case "snooze": return 4;
                default: return 9;
            }
        }

        private string GetStringSafe(string key, string fallback)
        {
            try
            {
                var s = PlayniteApi?.Resources?.GetString(key);
                if (string.IsNullOrWhiteSpace(s) || s == key)
                {
                    return fallback;
                }
                return s;
            }
            catch
            {
                return fallback;
            }
        }

        private string LocalizeState(string state)
        {
            switch (state)
            {
                case "online": return GetStringSafe("LOCSteamFriends_StateOnline", "Online");
                case "ingame": return GetStringSafe("LOCSteamFriends_StateInGame", "In game");
                case "away": return GetStringSafe("LOCSteamFriends_StateAway", "Away");
                case "busy": return GetStringSafe("LOCSteamFriends_StateBusy", "Busy");
                case "offline": return GetStringSafe("LOCSteamFriends_StateOffline", "Offline");
                case "snooze": return GetStringSafe("LOCSteamFriends_StateAway", "Away");
                default: return GetStringSafe("LOCSteamFriends_StateOffline", "Offline");
            }
        }



        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            if (!IsFullscreenMode())
            {
                return;
            }

            // 10-minute break (avoid refreshing during launch/game)
            pausedUntilUtc = DateTime.UtcNow.Add(PauseOnGameStart);
        }


        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            pausedUntilUtc = DateTime.MinValue;
            hasBaseline = false;

            if (IsFullscreenMode())
            {
                StartTimer(); 
            }
        }



        public override ISettings GetSettings(bool firstRunSettings) => settingsVm;

        public override UserControl GetSettingsView(bool firstRunSettings)
            => new SteamFriendsFullscreenSettingsView();
    }
}
