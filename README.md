# ASF-PluginTemplate

[![GitHub sponsor](https://img.shields.io/badge/GitHub-sponsor-ea4aaa.svg?logo=github-sponsors)](https://github.com/sponsors/JustArchi)
[![Patreon support](https://img.shields.io/badge/Patreon-support-f96854.svg?logo=patreon)](https://www.patreon.com/JustArchi)

[![Crypto donate](https://img.shields.io/badge/Crypto-donate-f7931a.svg?logo=bitcoin)](https://commerce.coinbase.com/checkout/0c23b844-c51b-45f4-9135-8db7c6fcf98e)
[![PayPal.me donate](https://img.shields.io/badge/PayPal.me-donate-00457c.svg?logo=paypal)](https://paypal.me/JustArchi)
[![PayPal donate](https://img.shields.io/badge/PayPal-donate-00457c.svg?logo=paypal)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=HD2P2P3WGS5Y4)
[![Revolut donate](https://img.shields.io/badge/Revolut-donate-0075eb.svg?logo=revolut)](https://pay.revolut.com/profile/ukaszyxm)
[![Steam donate](https://img.shields.io/badge/Steam-donate-000000.svg?logo=steam)](https://steamcommunity.com/tradeoffer/new/?partner=46697991&token=0ix2Ruv_)

---

[![Repobeats analytics image](https://repobeats.axiom.co/api/embed/4aa3ac833c7593826ac47ccfdc49c46ae27abb3d.svg "Repobeats analytics image")](https://github.com/JustArchiNET/ASF-PluginTemplate/pulse)

---

## Description

ASF-PluginTemplate is a template repository that you can use for creating custom **[plugins](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Plugins)** for **[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm)**. This template has everything needed to kickstart the structure of your custom ASF plugin. Most importantly, from viewpoint of a project not related to ASF whatsoever while making use of its best practices.

---

## How to use this template

Simply click the "Use this template" button in the top-right of the **[main repository page](https://github.com/JustArchiNET/ASF-PluginTemplate)** in order to get started.

For cloning your git repository, use `git clone --recursive` option in order to pull ASF reference along with your plugin, which you'll require during compilation. See **[git submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules)** for more info.

After using the template and cloning git repo, assuming you have everything required as specified in **[compilation](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Compilation)** page on our wiki, try to build your project with `dotnet build MyAwesomePlugin`, it should succeed without any issues, which means you're all set.

In theory, you don't need to do anything special further, just edit **[`MyAwesomePlugin/MyAwesomePlugin.cs`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/MyAwesomePlugin/MyAwesomePlugin.cs)** file and get going with your plugin logic. However, there are some things we recommend on doing in addition to above steps, and some we highlight as a possibility in case you'd be interested in them. It's now up to you what you want to do next.

---

## What's included

- Sample `MyAwesomePlugin` ASF plugin project with `ArchiSteamFarm` reference in git subtree.
- Seamless hook into the ASF build process, which simplifies the project structure, as you effectively inherit the default settings official ASF projects are built with. Of course, free to override.
- GitHub actions CI script, which verifies whether your project is possible to build. You can easily enhance it with unit tests when/if you'll have any.
- GitHub actions publish script, heavily inspired by ASF build process. Publish script allows you to `git tag` and `git push` selected tag, while CI will build, pack, create release on GitHub and upload the resulting artifacts, automatically. By default, it publishes `generic` and `generic-netf` variant of your plugin.
- GitHub actions ASF reference update script, which by default runs every day and ensures that your git submodule is tracking latest ASF (stable) release. Please note that this is a reference update only, the actual commit your plugin is built against is developer's responsibility not covered by this action, as it requires actual testing and verification. Because of that, commit created through this workflow can't possibly create any kind of build regression, it's a helper for you to more easily track latest ASF stable release.
- Configuration file for **[Renovate](https://github.com/renovatebot/renovate)** bot, which you can optionally decide to use. Using renovate, apart from bumping your library dependencies, can also cover bumping ASF commit that your plugin is built against, which together with above workflow will ensure that you're effectively tracking latest ASF (stable) release.
- Code style that matches the one we use at ASF, feel free to modify it to suit you.
- Other misc files for integration with `git` and GitHub.

---

## Recommended steps

Here we list steps that are **not mandatory**, but worthy to consider after using this repo as a template. While we'd recommend to cover all of those, it's totally alright if you don't. We ordered those according to our recommended priority.

- Choose license based on which you want to share your work. If you'd like to use the same one we do, so Apache 2.0, then you don't need to do anything as the plugin template comes with it. If you'd like to use different one, remove **[`LICENSE-2.0.txt`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/LICENSE-2.0.txt)** file and provide your own. If you've decided to use different license, it's probably also a good idea to update `PackageLicenseExpression` in **[`Directory.Build.props`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/Directory.Build.props#L17)**.
- Change this **[`README.md`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/README.md)** in any way you want to. You can check **[ASF's README](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/README.md)** for some inspiration. We recommend at least a short description of what your plugin can do. Updating `<Description>` in **[`Directory.Build.props`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/Directory.Build.props#L15)** also sounds like a good idea.
- Fill **[`SUPPORT.md`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/.github/SUPPORT.md)** file, so your users can learn where they can ask for help in regards to your plugin.
- Fill **[`SECURITY.md`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/.github/SECURITY.md)** file, so your users can learn where they should report critical security issues in regards to your plugin.
- If you want to use **[Renovate bot](https://github.com/renovatebot/renovate)** like we do, we recommend to modify the `:assignee()` block in our **[`renovate.json5`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/.github/renovate.json5#L5)** config file and putting your own GitHub username there. This will allow Renovate bot to assign failing PR to you so you can take a look at it. Everything else can stay as it is, unless you want to modify it of course.
- Provide your own **[`CODE_OF_CONDUCT.md`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/.github/CODE_OF_CONDUCT.md#enforcement)** if you'd like to. If you're fine with ours, you can simply replace `TODO@example.com` e-mail with your own.
- Provide your own **[`FUNDING.yml`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/.github/FUNDING.yml)** if you'd like to. By default the template comes with the funding available for the main ASF project, which you're free to keep, remove, or replace with your own.

---

## Worth mentioning

Here we list things that do not require your immediate attention, but we consider worthy to know.

### Compilation

Simply execute `dotnet build MyAwesomePlugin` and find your binaries in `MyAwesomePlugin/bin` folder, which you can drag to ASF's `plugins` folder. Keep in mind however that your plugin build created this way is based on existence of your .NET SDK and might not work on other machines or other SDK versions - for creating actual package with your plugin use `dotnet publish MyAwesomePlugin -c Release -o out` command instead, which will create a more general, packaged version in `out` directory. Likewise, omit `-c Release` if for some reason you'd like more general `Debug` build instead.

### Library references

Our plugin template uses centrally-managed packages. Simply add a `PackageVersion` reference below our `Import` clause in **[`Directory.Packages.props`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/Directory.Packages.props#L2)**. Afterwards add a `PackageReference` to your **[`MyAwesomePlugin.csproj`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/MyAwesomePlugin/MyAwesomePlugin.csproj#L6-L10)** as usual, but without specifying a version (which we've just specified in `Directory.Packages.props` instead).

Using centrally-managed NuGet packages is crucial in regards to integration with library versions used in the ASF submodule, especially the `System.Composition.AttributedModel` which your plugin should always have in the ASF matching version. This also means that you don't have to (and actually shouldn't) specify versions for all of the libraries that ASF defines on its own in **[`Directory.Packages.props`](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/Directory.Packages.props)** (that you conveniently inherit from).

### Renaming `MyAwesomePlugin`

You might be interested in renaming `MyAwesomePlugin` project into the one that suits your plugin. We've tried to keep the minimum amount of references, and we're listing here all of the places you should keep in mind:
- **[`MyAwesomePlugin.csproj`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/MyAwesomePlugin/MyAwesomePlugin.csproj)**, renaming should be enough.
- **[`MyAwesomePlugin.cs`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/MyAwesomePlugin/MyAwesomePlugin.cs#L6-L16)**, along with the update of `MyAwesomePlugin` class name (and included references to it).
- **[`MyAwesomePlugin.sln`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/MyAwesomePlugin.sln#L6)**, along with the update of `MyAwesomePlugin` reference in the `sln` file.
- **[`MyAwesomePlugin.sln.DotSettings`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/MyAwesomePlugin.sln.DotSettings)**, renaming to match the `sln` file above should be enough.
- **[`Directory.Build.props#`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/Directory.Build.props#L5)**, in particular `<PluginName>MyAwesomePlugin</PluginName>` line.
- **[`publish.yml`](https://github.com/JustArchiNET/ASF-PluginTemplate/blob/main/.github/workflows/publish.yml#L12)**, in particular `PLUGIN_NAME: MyAwesomePlugin` line.

Nothing else should be required to the best of our knowledge.

### Need help?

Feel free to ask in one of our **[support channels](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/.github/SUPPORT.md)**, where we'll be happy to offer you a helpful hand 😎.
