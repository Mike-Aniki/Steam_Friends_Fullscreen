
<div align="center">

# Steam Friends Fullscreen For Playnite
![Made for Playnite Fullscreen Themes](https://img.shields.io/badge/Made%20for-Playnite%20Fullscreen%20Themes-A600FF?style=for-the-badge)

</div>

Steam Friends Fullscreen is a Playnite plugin designed to display your Steam friends presence inside **Playnite Fullscreen mode**, with optional **Windows system notifications**.

It exposes **data, commands, and notification hooks** that Fullscreen themes can use to build a complete Steam friends UI.

Displays friend states: In-game, Online, Away, Busy, Offline (optional).

## Setup

1. Install the plugin from the Playnite Addons database or manually.
2. Open **Playnite → Add-ons → Extensions → Steam Friends Fullscreen**
3. Enter:
   - Your **Steam Web API Key**
   - Your **Steam profile URL or SteamID64**
4. (Optional) Enable **Show offline friends**
5. Use a **Fullscreen theme that supports the plugin**

**Important**

> _**Your Steam profile and Friends List must be set to Public.
> If either is private, the Steam Web API will return no data and the plugin will display outdated information or nothing at all.**_

This plugin does not provide any UI on its own.
A compatible Playnite Fullscreen theme is required to display its data.

## Notifications overview

Steam Friends Fullscreen can notify you when:
- A friend comes online
- A friend starts a game

Two independent systems are available:

- **Playnite notifications** → rendered & localized by the Fullscreen theme
- **Windows notifications** → rendered & localized by the plugin

## Theme Developers Guide

Steam Friends Fullscreen exposes everything through **PluginSettings**.
Themes are responsible for UI, layout, styling and animations.

## Global status bindings

| Property | Type | Description |
|--------|------|-------------|
| OnlineCount | int | Number of online friends |
| InGameCount | int | Number of friends currently in-game |
| OfflineCount | int | Number of offline friends |
| LastUpdateUtc | DateTime | Last successful refresh |
| LastError | string | Last error message (null if OK) |

**Example**

```xaml
<TextBlock Text="{PluginSettings Plugin=SteamFriendsFullscreen, Path=OnlineCount}" />
<TextBlock Text="{PluginSettings Plugin=SteamFriendsFullscreen, Path=InGameCount}" />
```

## Friends collection

```xaml
ItemsSource="{PluginSettings Plugin=SteamFriendsFullscreen, Path=Friends}"
```

Each friend item exposes:

| Property | Type | Description |
|--------|------|-------------|
| name | string | Steam display name |
| state | string | Raw state key (ingame, online, away, busy, snooze, offline) |
| stateLoc | string | Localized state label (theme-based, English fallback) |
| game | string | Current game (null if not in-game) |
| avatar | string | Local cached avatar URI or null |
| steamid | string | SteamID64 |

### Example – simple list

```xaml
<ListBox ItemsSource="{PluginSettings Plugin=SteamFriendsFullscreen, Path=Friends}">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal" Spacing="12">
                <Image Width="48" Height="48" Source="{Binding avatar}" />
                <StackPanel>
                    <TextBlock Text="{Binding name}" />
                    <TextBlock Text="{Binding stateLoc}" Opacity="0.7"/>
                </StackPanel>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

## Self user state (your own Steam status)

The plugin also exposes **your own Steam profile state**.

| Property | Type | Description |
|--------|------|-------------|
| SelfName | string | Your Steam display name |
| SelfState | string | Raw state key |
| SelfStateLoc | string | Localized label |
| SelfGame | string | Current game |
| SelfAvatar | string | Avatar URI |

### Example – display your status

```xaml
<TextBlock Text="{PluginSettings Plugin=SteamFriendsFullscreen, Path=SelfName}" />
<TextBlock Text="{PluginSettings Plugin=SteamFriendsFullscreen, Path=SelfStateLoc}"
           Opacity="0.8"/>
```

## User status control (commands)

Themes can let users **change their Steam status directly**.

### Exposed commands

| Command | Steam status |
|-------|--------------|
| SetStatusOnlineCommand | Online |
| SetStatusAwayCommand | Away |
| SetStatusBusyCommand | Busy |
| SetStatusOfflineCommand | Offline |
| SetStatusInvisibleCommand | Invisible |

### Example – status buttons

```xaml
<Button Content="Online"
        Command="{PluginSettings Plugin=SteamFriendsFullscreen, Path=SetStatusOnlineCommand}" />

<Button Content="Away"
        Command="{PluginSettings Plugin=SteamFriendsFullscreen, Path=SetStatusAwayCommand}" />

<Button Content="Invisible"
        Command="{PluginSettings Plugin=SteamFriendsFullscreen, Path=SetStatusInvisibleCommand}" />
```

**What the plugin handles**
- Sends `steam://friends/status/...`
- Updates `SelfState` and `SelfStateLoc`
- Syncs UI on next refresh

**_Steam must be running._**

## Steam client state & launch control

The plugin exposes the current Steam client state and a command to start Steam directly from a Fullscreen theme.

This allows themes to adapt their UI depending on whether Steam is running.

### Runtime property

| Property | Type | Description |
|----------|------|-------------|
| IsSteamRunning | bool | Indicates whether the Steam client is currently running |

Themes can use this property to:
- Hide Steam status buttons when Steam is not running
- Show a dedicated **Start Steam** button instead
- Disable Steam-related UI when unavailable

**Launch Steam command**

| Command | Description |
|--------|-------------|
| LaunchSteamCommand | Launches the Steam client if it is not running |

### Example

```xaml
<!-- Steam status buttons -->
<StackPanel>
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Visible"/>
            <Style.Triggers>
                <DataTrigger Binding="{PluginSettings Plugin=SteamFriendsFullscreen, Path=IsSteamRunning}"
                             Value="False">
                    <Setter Property="Visibility" Value="Collapsed"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>

    <!-- Status buttons here -->
</StackPanel>

<!-- Start Steam button -->
<Button Content="Start Steam"
        Command="{PluginSettings Plugin=SteamFriendsFullscreen, Path=LaunchSteamCommand}">
    <Button.Style>
        <Style TargetType="Button">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{PluginSettings Plugin=SteamFriendsFullscreen, Path=IsSteamRunning}"
                             Value="False">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

**Recommended UX pattern**

When IsSteamRunning = true
- show Steam status controls (Online / Away / Invisible / etc.)

When IsSteamRunning = false
- hide status controls
- show a single Start Steam button

This avoids presenting inactive Steam actions when the client is not running.

## Toast notifications (Fullscreen themes)

Themes receive toast events via PluginSettings.

### Runtime properties

| Property | Type | Description |
|--------|------|-------------|
| ToastIsVisible | bool | Toast visibility |
| ToastMessage | string | Formatted message |
| ToastAvatar | string | Friend avatar |
| ToastToken | long | Unique change token |
| ToastFlip | bool | Animation retrigger |

### Example – animated toast

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

## Windows notifications localization (plugin)

Windows notifications are localized **by the plugin**, using `Localization/*.xaml`.

| Key | Description |
|----|-------------|
| LOCSteamFriendsToast_OnlineShort | Online notification |
| LOCSteamFriendsToast_GameStartShort | Game start notification |
| LOCSteamFriends_StateOnline | State label |
| LOCSteamFriends_StateInGame | State label |
| LOCSteamFriends_StateOffline | State label |

Theme localization does **not** affect Windows notifications.

---

## Notes

- Refresh interval: **60 seconds**
- Refresh paused during gameplay
- Plugin UI runs only in Fullscreen mode
- Windows notifications work in Desktop or in-game
- Themes must handle null avatars and empty lists
