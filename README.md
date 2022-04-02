# ASF-FreeGames
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0) [![Plugin-ci](https://github.com/maxisoft/ASFFreeGames/actions/workflows/ci.yml/badge.svg)](https://github.com/maxisoft/ASFFreeGames/actions/workflows/ci.yml) [![Github All Releases](https://img.shields.io/github/downloads/maxisoft/ASFFreeGames/total.svg)]()

## Description

ASF-FreeGames is a **[plugin](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Plugins)** for **[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm)** allowing one to automatically **collect free steam games** posted on [Reddit](https://www.reddit.com/user/ASFinfo?sort=new).

---

## Requirements

- a working [ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm) environment

## Installation
- Download latest [Dll](https://github.com/maxisoft/ASFFreeGames/releases) from the release page
- Move the **dll** into the `plugins` folder of your *ArchiSteamFarm* installation
- (re)start ArchiSteamFarm
- Have fun

## How does it works
Every `30 minutes` the plugins starts analysing [reddit](https://www.reddit.com/user/ASFinfo?sort=new) for new **free games**.  
Then every `addlicense asf appid` commands found are broadcasted to each currently **logged bot**.

## Commands
- ```freegames``` to collect free games right now
- ```getip``` to get the ip used by ASF

---
## Dev notes

### Compilation

Simply execute `dotnet build ASFFreeGames -c Release` and find the dll in `ASFFreeGames/bin` folder, which you can drag to ASF's `plugins` folder.


[![GitHub sponsor](https://img.shields.io/badge/GitHub-sponsor-ea4aaa.svg?logo=github-sponsors)](https://github.com/sponsors/maxisoft)