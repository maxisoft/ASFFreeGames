# ASF-FreeGames

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0) [![Plugin-ci](https://github.com/maxisoft/ASFFreeGames/actions/workflows/ci.yml/badge.svg)](https://github.com/maxisoft/ASFFreeGames/actions/workflows/ci.yml) [![Github All Releases](https://img.shields.io/github/downloads/maxisoft/ASFFreeGames/total.svg)]()

## Description

ASF-FreeGames is a **[plugin](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Plugins)** for **[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm)** allowing one to automatically **collect free steam games** 🔑 posted on [Reddit](https://www.reddit.com/user/ASFinfo?sort=new).

---

## Requirements

- ✅ a working [ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm) environment

## Installation

- 🔽 Download latest [Dll](https://github.com/maxisoft/ASFFreeGames/releases) from the release page
- ➡️ Move the **dll** into the `plugins` folder of your _ArchiSteamFarm_ installation
- 🔄 (re)start ArchiSteamFarm
- 🎉 Have fun

## How does it work

Every ⏰`30 minutes` the plugin starts 🔬analyzing [reddit](https://www.reddit.com/user/ASFinfo?sort=new) for new **free games**⚾.
Then every 🔑`addlicense asf appid` command found is broadcasted to each currently **logged bot** 💪.

## Commands

- `freegames` to collect free games right now 🚀
- `getip` to get the IP used by ASF 👀
- `set` to configure this plugin's options (see below) 🛠️

For information about issuing 📢commands see [ASF's wiki](https://github.com/JustArchiNET/ArchiSteamFarm/wiki)

### Advanced configuration

The plugin behavior is configurable via command

- `freegames set nof2p` to ⛔**prevent** the plugin from collecting **free to play** games
- `freegames set f2p` to ☑️**allow** the plugin to collect **f2p** (the default)
- `freegames set nodlc` to ⛔**prevent** the plugin from collecting **dlc**
- `freegames set dlc` to ☑️**allow** the plugin to collect **dlc** (the default)
- `freegames set clearblacklist` to 🗑️**clear** all entries from the **blacklist**
- `freegames set removeblacklist s/######` to 🔄**remove** a specific package from the **blacklist**
- `freegames set showblacklist` to 📋**display** all entries in the current **blacklist**

In addition to the commands above, the configuration is stored in a 📖`config/freegames.json.config` JSON file, which one may 🖊 edit using a text editor to suit their needs.

#### Additional Configuration Options

The following options can be set in the `freegames.json.config` file:

```json
{
	"autoBlacklistForbiddenPackages": true, // Automatically blacklist packages that return Forbidden errors
	"delayBetweenRequests": 500, // Delay in milliseconds between license requests (helps avoid rate limits)
	"maxRetryAttempts": 1, // Number of retry attempts for transient errors (like timeouts)
	"retryDelayMilliseconds": 2000 // Delay in milliseconds before retrying a failed request
}
```

**Option Descriptions:**

- `autoBlacklistForbiddenPackages`: When true, packages that return "Forbidden" errors are automatically added to the blacklist to prevent future attempts.
- `delayBetweenRequests`: Adds a delay between license requests to reduce the chance of hitting Steam's rate limits.
- `maxRetryAttempts`: Number of times to retry requests that fail due to transient errors (e.g., timeouts).
- `retryDelayMilliseconds`: How long to wait before retrying a failed request.

## Proxy Setup

The plugin can be configured to use a proxy (HTTP(S), SOCKS4, or SOCKS5) for its HTTP requests to Reddit. You can achieve this in two ways:

1. **Environment Variable:** Set the environment variable `FREEGAMES_RedditProxy` with your desired proxy URL (e.g., `http://yourproxy:port`).
2. **`freegames.json.config`:** Edit the `redditProxy` property within the JSON configuration file located at `<asf>/config/freegames.json.config`. Set the value to your proxy URL.

**Example `freegames.json.config` with Proxy:**

```json
{
...
  "redditProxy": "http://127.0.0.1:1080"
}
```

**Important Note:** If you pass a proxy **password**, it will be **stored in clear text** in the `freegames.json.config` file, even when passing it via the environment variable.

**Note:** Whichever method you choose (environment variable or config file), only one will be used at a time.
The environment variable takes precedence over the config file setting.

## FAQ

### Log is full of `Request failed after 5 attempts!` messages is there something wrong ?

- There's nothing wrong (most likely), those error messages are the result of the plugin trying to add a steam key which is unavailable. With time those errors should occurs less frequently (see [#3](https://github.com/maxisoft/ASFFreeGames/issues/3) for more details).

### How to configure automatic updates for the plugin?

The plugin supports checking for updates on GitHub. You can enable automatic updates by modifying the `PluginsUpdateList` property in your ArchiSteamFarm configuration (refer to the [ArchiSteamFarm wiki](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Configuration#pluginsupdatelist) for details).

**Important note:** Enabling automatic updates for plugins can have security implications. It's recommended to thoroughly test updates in a non-production environment before enabling them on your main system.

---

## Dev notes

### Compilation

Simply execute `dotnet build ASFFreeGames -c Release` and find the dll in `ASFFreeGames/bin` folder, which you can drag to ASF's `plugins` folder.

[![GitHub sponsor](https://img.shields.io/badge/GitHub-sponsor-ea4aaa.svg?logo=github-sponsors)](https://github.com/sponsors/maxisoft)
