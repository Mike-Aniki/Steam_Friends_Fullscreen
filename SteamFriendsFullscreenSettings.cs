using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;


namespace SteamFriendsFullscreen
{
    public enum NotificationOutputMode
    {
        Off = 0,
        PlayniteOnly = 1,
        WindowsOnly = 2,
        PlayniteAndWindows = 3
    }

    public class SteamFriendsFullscreenSettings : ObservableObject
    {
        public int RefreshSeconds => 60;
        public int MaxFriendsShown => 15;
        public int MaxOfflineShown => 40;

        private string steamApiKey = string.Empty;
        private string steamId64 = string.Empty;

        // Offline toggle 
        private bool showOffline = false;

        // ===== Notifications (saved) =====
        // Output mode: Off / Playnite / Windows / Both
        private NotificationOutputMode notificationOutputMode = NotificationOutputMode.PlayniteOnly;

        public NotificationOutputMode NotificationOutputMode
        {
            get => notificationOutputMode;
            set => SetValue(ref notificationOutputMode, value);
        }

        // Default: notify when a friend starts a game
        private bool notifyOnGameStart = true;

        // Optional: notify when a friend comes online
        private bool notifyOnConnect = false;

        

        public bool NotifyOnGameStart
        {
            get => notifyOnGameStart;
            set => SetValue(ref notifyOnGameStart, value);
        }

        public bool NotifyOnConnect
        {
            get => notifyOnConnect;
            set => SetValue(ref notifyOnConnect, value);
        }


        public string SteamApiKey
        {
            get => steamApiKey;
            set => SetValue(ref steamApiKey, value);
        }

        public string SteamId64
        {
            get => steamId64;
            set => SetValue(ref steamId64, value);
        }

        public bool ShowOffline
        {
            get => showOffline;
            set => SetValue(ref showOffline, value);
        }

        public SteamFriendsFullscreenSettings()
        {
            showOffline = false;
            Friends = new ObservableCollection<FriendPresenceDto>();
            notificationOutputMode = NotificationOutputMode.PlayniteOnly;
            notifyOnGameStart = true;
            notifyOnConnect = false;

        }

        // ===== Toast (runtime, theme-driven) =====
        private bool toastIsVisible;
        private string toastMessage;
        private string toastAvatar;
        private long toastToken;
        private bool toastFlip;

        [DontSerialize]
        public bool ToastIsVisible
        {
            get => toastIsVisible;
            set => SetValue(ref toastIsVisible, value);
        }

        [DontSerialize]
        public bool ToastFlip
        {
            get => toastFlip;
            set => SetValue(ref toastFlip, value);
        }

        [DontSerialize]
        public string ToastMessage
        {
            get => toastMessage;
            set => SetValue(ref toastMessage, value);
        }

        [DontSerialize]
        public string ToastAvatar
        {
            get => toastAvatar;
            set => SetValue(ref toastAvatar, value);
        }

        [DontSerialize]
        public long ToastToken
        {
            get => toastToken;
            set => SetValue(ref toastToken, value);
        }


        public void EnsureRuntimeCollections()
        {
            if (Friends == null)
            {
                Friends = new ObservableCollection<FriendPresenceDto>();
            }
        }

        [DontSerialize]
        public ObservableCollection<FriendPresenceDto> Friends { get; private set; }

        // Runtime 
        private int onlineCount = 0;
        private int inGameCount = 0;
        private int offlineCount = 0;

        private DateTime lastUpdateUtc = DateTime.MinValue;
        private string lastError = null;

        [DontSerialize]
        public DateTime LastUpdateUtc
        {
            get => lastUpdateUtc;
            set => SetValue(ref lastUpdateUtc, value);
        }

        [DontSerialize]
        public string LastError
        {
            get => lastError;
            set => SetValue(ref lastError, value);
        }

        [DontSerialize]
        public bool IsStale
        {
            get
            {
                if (LastUpdateUtc == DateTime.MinValue)
                {
                    return true;
                }

                var staleAfter = TimeSpan.FromSeconds(Math.Max(90, RefreshSeconds * 3));
                return (DateTime.UtcNow - LastUpdateUtc) > staleAfter;
            }
        }

        [DontSerialize]
        public int OnlineCount
        {
            get => onlineCount;
            set => SetValue(ref onlineCount, value);
        }

        [DontSerialize]
        public int InGameCount
        {
            get => inGameCount;
            set => SetValue(ref inGameCount, value);
        }

        [DontSerialize]
        public int OfflineCount
        {
            get => offlineCount;
            set => SetValue(ref offlineCount, value);
        }

        [DontSerialize]
        public string PluginStatus => "OK";

        // ===== Self (my profile) runtime =====
        private string selfName = null;
        private string selfState = "offline";
        private string selfGame = null;
        private string selfAvatar = null;
        private string selfStateLoc = "Offline";

        [DontSerialize]
        public string SelfStateLoc
        {
            get => selfStateLoc;
            set => SetValue(ref selfStateLoc, value);
        }


        [DontSerialize]
        public string SelfName
        {
            get => selfName;
            set => SetValue(ref selfName, value);
        }

        [DontSerialize]
        public string SelfState
        {
            get => selfState;
            set => SetValue(ref selfState, value);
        }

        [DontSerialize]
        public string SelfGame
        {
            get => selfGame;
            set => SetValue(ref selfGame, value);
        }

        [DontSerialize]
        public string SelfAvatar
        {
            get => selfAvatar;
            set => SetValue(ref selfAvatar, value);
        }
        [DontSerialize]
        public Action DebugTestNotification { get; set; }

        // ===== Commands (runtime) =====
        [DontSerialize] public ICommand SetStatusOnlineCommand { get; set; }
        [DontSerialize] public ICommand SetStatusAwayCommand { get; set; }
        [DontSerialize] public ICommand SetStatusBusyCommand { get; set; }
        [DontSerialize] public ICommand SetStatusInvisibleCommand { get; set; }
        [DontSerialize] public ICommand SetStatusOfflineCommand { get; set; }

    }

    public class SteamFriendsFullscreenSettingsViewModel : ObservableObject, ISettings
    {
        private readonly SteamFriendsFullscreen plugin;
        private SteamFriendsFullscreenSettings editingClone;

        private SteamFriendsFullscreenSettings settings;
        public SteamFriendsFullscreenSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public NotificationOutputMode NotificationOutputMode
        {
            get => Settings.NotificationOutputMode;
            set
            {
                if (Settings.NotificationOutputMode == value)
                {
                    return;
                }

                Settings.NotificationOutputMode = value;
                OnPropertyChanged();

                // Start/Stop timer immediately based on the new mode + current app mode
                plugin.StartTimer(); // StartTimer() already contains the logic to StopTimer() if not needed
            }
        }


        public SteamFriendsFullscreenSettingsViewModel(SteamFriendsFullscreen plugin)
        {
            this.plugin = plugin;

            var savedSettings = plugin.LoadPluginSettings<SteamFriendsFullscreenSettings>();
            Settings = savedSettings ?? new SteamFriendsFullscreenSettings();
            // Migration (ancienne version -> nouvelle)
            if (savedSettings != null)
            {
                // Si l'ancien setting EnableNotifications existait, on essaye de garder un comportement équivalent.
                // (Si tu as supprimé complètement EnableNotifications, tu peux retirer ce try/catch.)
                try
                {
                    // Si NotificationOutputMode est resté à Off, on le remet comme avant.
                    if (Settings.NotificationOutputMode == NotificationOutputMode.Off)
                    {
                        Settings.NotificationOutputMode = NotificationOutputMode.PlayniteOnly;
                    }
                }
                catch { }
            }

            Settings.EnsureRuntimeCollections();
            Settings.DebugTestNotification = () =>
            {
                plugin.DebugTriggerTestNotification();
            };

            Settings.SetStatusOnlineCommand = new SimpleCommand(() => plugin.SetSteamStatus("online"));
            Settings.SetStatusAwayCommand = new SimpleCommand(() => plugin.SetSteamStatus("away"));
            Settings.SetStatusBusyCommand = new SimpleCommand(() => plugin.SetSteamStatus("busy"));
            Settings.SetStatusInvisibleCommand = new SimpleCommand(() => plugin.SetSteamStatus("invisible"));
            Settings.SetStatusOfflineCommand = new SimpleCommand(() => plugin.SetSteamStatus("offline"));


        }

        public void BeginEdit()
        {
            Settings.EnsureRuntimeCollections();
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
            Settings.EnsureRuntimeCollections();

            OnPropertyChanged(nameof(NotificationOutputMode));
            plugin.StartTimer();
        }


        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);

            OnPropertyChanged(nameof(NotificationOutputMode));
            plugin.StartTimer();
        }



        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            return true;
        }
    }
}
