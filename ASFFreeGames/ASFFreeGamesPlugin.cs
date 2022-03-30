using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm;
using ArchiSteamFarm.Collections;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SteamKit2;
using static ArchiSteamFarm.Core.ASF;

namespace Maxisoft.ASF;

#pragma warning disable CA1812 // ASF uses this class during runtime
[UsedImplicitly]
internal sealed class ASFFreeGamesPlugin : IASF, IBot, IBotConnection {
	public string Name => nameof(ASFFreeGamesPlugin);
	public Version Version => typeof(ASFFreeGamesPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	private readonly ConcurrentHashSet<Bot> Bots = new(new BotEqualityComparer());
	private readonly ConcurrentDictionary<string, BotContext> BotContexts = new();
	private readonly RedditHelper RedditHelper = new();
	private readonly SemaphoreSlim SemaphoreSlim = new(1, 1);

	private Timer? Timer;

	public Task OnLoaded() {
		//ArchiLogger.LogGenericInfo($"Loaded {Name}", nameof(OnLoaded));
		return Task.CompletedTask;
	}

	public Task OnASFInit(IReadOnlyDictionary<string, JToken>? additionalConfigProperties = null) => Task.CompletedTask;

	public async Task OnBotDestroy(Bot bot) => await RemoveBot(bot).ConfigureAwait(false);

	public Task OnBotInit(Bot bot) => Task.CompletedTask;

	public async Task OnBotDisconnected(Bot bot, EResult reason) => await RemoveBot(bot).ConfigureAwait(false);

	private void ResetTimer(Timer? newTimer = null) {
		Timer?.Dispose();
		Timer = newTimer;
	}

	private async Task RemoveBot(Bot bot) {
		Bots.Remove(bot);

		if (BotContexts.TryRemove(bot.BotName, out var ctx)) {
			await ctx.Save().ConfigureAwait(false);
		}

		if ((Bots.Count == 0)) {
			ResetTimer();
		}
	}

	private async Task RegisterBot(Bot bot) {
		Bots.Add(bot);

		if (Timer is null) {
			var delay = TimeSpan.FromMilliseconds(GlobalDatabase?.LoadFromJsonStorage($"{Name}.timer")?.ToObject<double>() ?? TimeSpan.FromMinutes(30).TotalMilliseconds);
			ResetTimer(new Timer(CollectGames));
			Timer?.Change(TimeSpan.FromSeconds(10), delay);
		}

		if (!BotContexts.TryGetValue(bot.BotName, out var ctx)) {
			lock (BotContexts) {
				if (!BotContexts.TryGetValue(bot.BotName, out ctx)) {
					ctx = BotContexts[bot.BotName] = new BotContext(bot);
				}
			}
		}

		await ctx.Load().ConfigureAwait(false);
	}

	public async Task OnBotLoggedOn(Bot bot) => await RegisterBot(bot).ConfigureAwait(false);

	private async void CollectGames(object? source) {
		if (!await SemaphoreSlim.WaitAsync(100).ConfigureAwait(false)) {
			return;
		}

		try {
			var games = await RedditHelper.ListGames().ConfigureAwait(false);

			ArchiLogger.LogGenericInfo($"found {games.Count} games", nameof(CollectGames));

			foreach (Bot bot in Bots) {
				if (!bot.IsConnectedAndLoggedOn) {
					continue;
				}

				if (bot.GamesToRedeemInBackgroundCount > 0) {
					continue;
				}

				bool save = false;
				BotContext context = BotContexts[bot.BotName];
				string message = string.Join(",", games.Where(static g => !g.FreeToPlay).Select(static g => g.Identifier).Where(id => !context.HasApp(id)));
				ArchiLogger.LogGenericDebug($"collecting games on {bot.BotName} {message}", nameof(CollectGames));

				foreach ((string? identifier, bool freeToPlay, long time) in games) {
					if (freeToPlay) {
						continue;
					}

					if (context.HasApp(identifier)) {
						continue;
					}

					string? resp = await bot.Commands.Response(EAccess.Operator, $"ADDLICENSE {bot.BotName} {identifier}").ConfigureAwait(false);
					bool success = false;

					if (!string.IsNullOrWhiteSpace(resp)) {
						ArchiLogger.LogGenericInfo($"{resp}", nameof(CollectGames));
						success = resp!.Contains("collected game", StringComparison.InvariantCultureIgnoreCase);
						success |= resp!.Contains("OK", StringComparison.InvariantCultureIgnoreCase);
					}

					if (success) {
						lock (context) {
							context.SaveApp(identifier);
						}

						save = true;
					}
					else if (Math.Abs(Utilities.GetUnixTime() - time) > TimeSpan.FromHours(24).TotalMilliseconds) {
						context.AppTickCount(identifier, increment: true);
					}
				}

				if (save) {
					await context.Save().ConfigureAwait(false);
				}
			}
		}
		finally {
			SemaphoreSlim.Release();
		}
	}
}
#pragma warning restore CA1812 // ASF uses this class during runtime
