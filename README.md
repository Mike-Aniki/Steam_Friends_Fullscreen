
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

# For Fullscreen theme developers, a complete integration guide is available in the [wiki](https://github.com/Mike-Aniki/Steam_Friends_Fullscreen/wiki/Theme-Developers-Guide)
