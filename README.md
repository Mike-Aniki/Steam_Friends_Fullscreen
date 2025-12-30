<div align="center">

# Steam Friends Fullscreen For Playnite
![Made for Playnite Fullscreen Themes](https://img.shields.io/badge/Made%20for-Playnite%20Fullscreen%20Themes-A600FF?style=for-the-badge)

</div>


Steam Friends Fullscreen is a Playnite plugin designed to display your Steam friends presence directly inside Playnite Fullscreen mode.

Displays your Steam friends presence: In-game, Online, Away, Busy, Offline (optional).

The plugin runs exclusively in Playnite Fullscreen mode.

## Setup

1. Install the plugin from the Playnite Addons database or manually.
2. Open Playnite → Add-ons → Extensions → Steam Friends Fullscreen
3. Enter:
   - Your Steam Web API Key
   - Your Steam profile URL or SteamID64
4. (Optional) Enable "Show offline friends"
5. Use a Fullscreen theme that supports the plugin.

Note:  
This plugin does not render any UI by itself in Fullscreen.  
A compatible Fullscreen theme is required.

---

## Theme Developers Guide

Steam Friends Fullscreen exposes all its data through PluginSettings.
Fullscreen themes are responsible for rendering and interaction.

#### Global status

| Property        | Type     | Description                          |
|-----------------|----------|--------------------------------------|
| OnlineCount     | int      | Number of online friends             |
| InGameCount     | int      | Number of friends currently in-game  |
| OfflineCount    | int      | Number of offline friends            |
| LastUpdateUtc   | DateTime | Last successful refresh              |
| LastError       | string   | Last error message (null if OK)      |
| IsStale         | bool     | Indicates outdated data              |

---

### Friends collection

```xaml
ItemsSource="{PluginSettings Plugin=SteamFriendsFullscreen, Path=Friends}"
```

Each item in the collection exposes the following properties:

| Property | Type   | Description |
|---------|--------|-------------|
| name    | string | Steam display name |
| state   | string | Raw friend state key (for logic & styling): ingame, online, away, busy, snooze, offline |
| stateLoc | string | Localized state label (resolved by the plugin, with English fallback) |
| game    | string | Current game name (null when not in-game) |
| avatar  | string | Local cached avatar URI or null |
| steamid | string | SteamID64 |

Themes should use `stateLoc` for display purposes.
The `state` property is intended for triggers, styling and logic only.

---


### Example

```xaml
<ListBox ItemsSource="{PluginSettings Plugin=SteamFriendsFullscreen, Path=Friends}">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding name}" />
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```
### Localization

Steam Friends Fullscreen **does not ship any localization files**.

The plugin exposes **localization keys**, which are resolved using Playnite’s resource system.
If a key is **not defined by the theme**, the plugin **falls back to English** automatically.

### Supported localization keys

| Loc key | English fallback | Description |
|-------|------------------|-------------|
| LOCSteamOnline | Online | Online status label |
| LOCSteamInGame | In game | In-game status label |
| LOCSteamAway | Away | Away status label |
| LOCSteamBusy | Busy | Busy status label |
| LOCSteamSnooze | Snooze | Snooze status label |
| LOCSteamOffline | Offline | Offline status label |

Example usage in a fullscreen theme:

```xaml
<TextBlock Text="{Binding stateLoc}" />
```
This automatically displays the localized label (or English fallback if missing).

---

### Important notes

- Refresh interval is fixed to 60 seconds.
- All activity is paused automatically when a game starts.
- The plugin runs only in Fullscreen mode.
- Themes should handle empty lists and null avatars.
