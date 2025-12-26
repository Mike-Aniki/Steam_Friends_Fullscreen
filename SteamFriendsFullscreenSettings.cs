using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SteamFriendsFullscreen
{
    public class SteamFriendsFullscreenSettings : ObservableObject
    {
        public int RefreshSeconds => 60;
        public int MaxFriendsShown => 15;
        public int MaxOfflineShown => 40;

        private string steamApiKey = string.Empty;
        private string steamId64 = string.Empty;

        // Offline toggle 
        private bool showOffline = false;

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

        public SteamFriendsFullscreenSettingsViewModel(SteamFriendsFullscreen plugin)
        {
            this.plugin = plugin;

            var savedSettings = plugin.LoadPluginSettings<SteamFriendsFullscreenSettings>();
            Settings = savedSettings ?? new SteamFriendsFullscreenSettings();
            Settings.EnsureRuntimeCollections();
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
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            return true;
        }
    }
}
