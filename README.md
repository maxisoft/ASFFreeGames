# ASF-FreeGames
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0) [![Plugin-ci](https://github.com/maxisoft/ASFFreeGames/actions/workflows/ci.yml/badge.svg)](https://github.com/maxisoft/ASFFreeGames/actions/workflows/ci.yml) [![Github All Releases](https://img.shields.io/github/downloads/maxisoft/ASFFreeGames/total.svg)]()

## Description

ASF-FreeGames is a **[plugin](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Plugins)** for **[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm)** allowing one to automatically **collect free steam games** 🔑 posted on [Reddit](https://www.reddit.com/user/ASFinfo?sort=new).

---

## Requirements

- ✅ a working [ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm) environment

## Installation
- 🔽 Download latest [Dll](https://github.com/maxisoft/ASFFreeGames/releases) from the release page
- ➡️ Move the **dll** into the `plugins` folder of your *ArchiSteamFarm* installation
- 🔄 (re)start  ArchiSteamFarm
- 🎉 Have fun

## How does it works
Every ⏰`30 minutes` the plugins starts 🔬analysing [reddit](https://www.reddit.com/user/ASFinfo?sort=new) for new **free games**⚾.  
Then every 🔑`addlicense asf appid`  commands found are broadcasted to each currently **logged bot** 💪.

## Commands
- ```freegames``` to collect free games right now 🚀
- ```getip``` to get the ip used by ASF 👀
- ```set``` to configure this plugin options (see below) 🛠️

for information about issuing 📢commands see [ASF's wiki](https://github.com/JustArchiNET/ArchiSteamFarm/wiki)

### Advanced configuration
The plugin behavior is configurable via command
- ```freegames set nof2p``` to ⛔**prevent** the plugin to collect **free to play** games
- ```freegames set f2p``` to ☑️**allow** the plugin to collect **f2p** (the default)
- ```freegames set nodlc``` to ⛔**prevent** the plugin to collect **dlc**
- ```freegames set dlc``` to ☑️**allow** the plugin to collect **dlc** (the default)

In addition to the command above, the configuration is stored in a 📖```config/freegames.json.config``` json file, one may 🖊 edit it using a text editor to suit its need.


## FAQ

### Log is full of `Request failed after 5 attempts!` messages is there something wrong ?   

- There's nothing wrong (most likely), those error messages are the result of the plugin trying to add a steam key which is unavailable. With time those errors should occurs less frequently (see [#3](https://github.com/maxisoft/ASFFreeGames/issues/3) for more details).
---
## Dev notes

### Compilation

Simply execute `dotnet build ASFFreeGames -c Release` and find the dll in `ASFFreeGames/bin` folder, which you can drag to ASF's `plugins` folder.


[![GitHub sponsor](https://img.shields.io/badge/GitHub-sponsor-ea4aaa.svg?logo=github-sponsors)](https://github.com/sponsors/maxisoft)