<div align="center">

# Steam Friends Fullscreen For Playnite
![Made for Playnite Fullscreen Themes](https://img.shields.io/badge/Made%20for-Playnite%20Fullscreen%20Themes-A600FF?style=for-the-badge)

</div>

Steam Friends Fullscreen is a Playnite plugin designed to display your Steam friends presence inside **Playnite Fullscreen mode**, with optional **Windows system notifications**.

It exposes **data, commands, and notification hooks** that Fullscreen themes can use to build a complete Steam friends UI.

Displays friend states: In-game, Online, Away, Busy and Offline (optional).

## Setup

1. Install the plugin from the Playnite Add-ons database or manually.
2. Open **Playnite → Add-ons → Extensions → Steam Friends Fullscreen**.
3. Choose how you want to connect to Steam:
   - **Connect with Steam using the QR code** — recommended.
   - **Use a Steam Web API key** instead.
   - **Use both** — recommended for automatic backup.
4. (Optional) Enable **Show offline friends**.
5. Use a **Fullscreen theme that supports the plugin**.

### Connection options

#### Connect with Steam — recommended

- Quick connection using a QR code.
- No Steam Web API key required.
- Your Steam friends list does not need to be public.
- You may occasionally need to reconnect if the Steam session expires.

#### Steam Web API key

- Can be used without the QR code connection.
- Can automatically take over if the Steam connection becomes unavailable.
- Less likely to require reconnection.
- Requires a Steam Web API key and your Steam profile URL or SteamID64.
- Your Steam friends list must be public while this method is being used.

> **Recommended setup:** connect with Steam using the QR code and optionally add an API key as a backup. The plugin always tries the Steam connection first and uses the API key only if needed.

This plugin does not provide a Fullscreen friends interface on its own.  
A compatible Playnite Fullscreen theme is required to display its data.

## Notifications overview

Steam Friends Fullscreen can notify you when:

- A friend comes online.
- A friend starts a game.

Two independent systems are available:

- **Playnite notifications** → rendered and localized by the Fullscreen theme.
- **Windows notifications** → rendered and localized by the plugin.

# For Fullscreen theme developers, a complete integration guide is available in the [wiki](https://github.com/Mike-Aniki/Steam_Friends_Fullscreen/wiki/Theme-Developers-Guide)

**Support me on Ko-fi**

<a href='https://ko-fi.com/W7W1Y9DRB' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi5.png?v=3' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

</div>
