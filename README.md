# ASF-FreeGames

[![GitHub sponsor](https://img.shields.io/badge/GitHub-sponsor-ea4aaa.svg?logo=github-sponsors)](https://github.com/sponsors/maxisoft)

## Description

ASF-FreeGames is a **[plugin](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Plugins)** for **[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm)** allowing one to automatically **collect free steam games** posted on [Reddit](https://www.reddit.com/user/ASFinfo?sort=new).

---

## Requirements

- a Working [ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm) environment

## Installation
- Download latest [Dll](https://github.com/maxisoft/ASFFreeGames/releases) from the release page
- Move the **dll** into the `plugins` of your *ArchiSteamFarm* folder
- (re)start ArchiSteamFarm
- Have fun

---
## Dev notes

### Compilation

Simply execute `dotnet build ASFFreeGames -c Release` and find the dll in `ASFFreeGames/bin` folder, which you can drag to ASF's `plugins` folder.