<div align="center">

# Steam Friends Fullscreen For Playnite
![Made for Playnite Fullscreen Themes](https://img.shields.io/badge/Made%20for-Playnite%20Fullscreen%20Themes-A600FF?style=for-the-badge)

</div>


Steam Friends Fullscreen is a Playnite plugin designed to display your Steam friends presence directly inside Playnite Fullscreen mode.

Displays your Steam friends presence: In-game, Online, Away, Busy, Offline (optional).

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

## Notifications (Toast)

Steam Friends Fullscreen can notify you when a friend connects or launches a game.
Notifications can be shown in different ways depending on the selected mode.

**Playnite notifications**
>- Notifications appear **only inside Playnite Fullscreen**
>- They are displayed and animated by the Fullscreen theme
>- **This mode works only in Fullscreen mode and requires a compatible Fullscreen theme**

_Use this if:_
_You play exclusively in Playnite Fullscreen and want fully integrated, in-theme notifications._

**Windows notifications**
>- Notifications use the **Windows system notification center**
>- They can appear anytime, depending on Windows settings:
>  - in Desktop mode or Fullscreen mode
>  - when Playnite is minimized
>  - while a game is running
>- Playnite must be running in the background

_Use this if:_
_You want to receive notifications at all times, even when Playnite is not visible._

**Playnite + Windows**
>- Notifications appear **both**:
>  - inside the Fullscreen theme
>  - as Windows system notifications

_Use this if:_ 
_you want notifications in Fullscreen *and* outside Playnite._

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

## Notifications

Steam Friends Fullscreen can trigger **runtime toast notifications** when a friend's status changes.

The plugin **does not render any toast UI by itself**.  
Notifications are fully **theme-driven** and exposed via `PluginSettings`.

### Toast runtime properties (theme binding)

| Property | Type | Description |
|--------|------|-------------|
| ToastIsVisible | bool | Indicates when a toast should be shown |
| ToastMessage | string | Notification message (already formatted) |
| ToastAvatar | string | Friend avatar URI or null |
| ToastToken | long | Unique value updated per toast (useful for animations) |
| ToastFlip | bool | Toggle used to trigger animations |

ToastFlip toggles on every notification and should be used to retrigger animations,
even if the toast is already visible.

### Example (animated toast)

Use `ToastFlip` to retrigger the animation on every notification.

```xaml
<Border Opacity="0">
    <Border.RenderTransform>
        <TranslateTransform X="300"/>
    </Border.RenderTransform>

    <Border.Style>
        <Style TargetType="Border">
            <Style.Triggers>
                <DataTrigger Binding="{PluginSettings Plugin=SteamFriendsFullscreen, Path=ToastFlip}"
                             Value="True">
                    <DataTrigger.EnterActions>
                        <BeginStoryboard>
                            <Storyboard>
                                <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                                 From="0" To="1"
                                                 Duration="0:0:0.15"/>
                                <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)"
                                                 From="300" To="0"
                                                 Duration="0:0:0.25"/>
                                <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                                 BeginTime="0:0:5"
                                                 From="1" To="0"
                                                 Duration="0:0:0.2"/>
                            </Storyboard>
                        </BeginStoryboard>
                    </DataTrigger.EnterActions>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Border.Style>

    <TextBlock Text="{PluginSettings Plugin=SteamFriendsFullscreen, Path=ToastMessage}"/>
</Border>
```
The animation will replay every time ToastFlip changes.

### Localization keys related to notifications

ToastMessage can be localized by theme, with English as the fallback language.

| Loc key | English fallback | Description |
|--------|------------------|-------------|
| LOCSteamFriendsToast_GameStart | "{0} started playing {1}" | Friend game start notification |
| LOCSteamFriendsToast_Online | "{0} is now {1}" | Friend connection notification |

---

### Important notes

- Refresh interval is fixed to 60 seconds.
- All activity is paused automatically when a game starts.
- The plugin runs only in Fullscreen mode.
- Themes should handle empty lists and null avatars.
