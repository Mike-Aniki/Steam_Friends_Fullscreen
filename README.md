# Steam Friends Fullscreen For Playnite

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
| state   | string | Friend state (string enum): ingame, online, away, busy, snooze, offline |
| game    | string | Current game name (null when not in-game) |
| avatar  | string | Local cached avatar URI or null |
| steamid | string | SteamID64 |

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

---

### Important notes

- Refresh interval is fixed to 60 seconds.
- All activity is paused automatically when a game starts.
- The plugin runs only in Fullscreen mode.
- Themes should handle empty lists and null avatars.
