using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm;
using ArchiSteamFarm.Collections;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Interaction;
using JetBrains.Annotations;
using Maxisoft.ASF.Reddit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamKit2;
using static ArchiSteamFarm.Core.ASF;

namespace Maxisoft.ASF;

#pragma warning disable CA1812 // ASF uses this class during runtime
[UsedImplicitly]
internal sealed class ASFFreeGamesPlugin : IASF, IBot, IBotConnection, IBotCommand2 {
	private const int CollectGamesTimeout = 3 * 60 * 1000;
	public string Name => nameof(ASFFreeGamesPlugin);
	public Version Version => typeof(ASFFreeGamesPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	private readonly ConcurrentHashSet<Bot> Bots = new(new BotEqualityComparer());
	private readonly ConcurrentDictionary<string, BotContext> BotContexts = new();
	private readonly RedditHelper RedditHelper = new();
	private readonly SemaphoreSlim SemaphoreSlim = new(1, 1);
	private readonly Lazy<CancellationTokenSource> CancellationTS = new(static () => new CancellationTokenSource());

	private Timer? Timer;

	public Task OnLoaded() {
		//ArchiLogger.LogGenericInfo($"Loaded {Name}", nameof(OnLoaded));
		return Task.CompletedTask;
	}

	public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) {
		string formatBotResponse(string resp) {
			return bot?.Commands?.FormatBotResponse(resp) ?? Commands.FormatStaticResponse(resp);
		}

		if (args is { Length: > 0 } && (args[0]?.ToUpperInvariant() == "GETIP")) {
			var webBrowser = bot?.ArchiWebHandler?.WebBrowser ?? WebBrowser;

			if (webBrowser is null) {
				return formatBotResponse(string.Format(CultureInfo.CurrentCulture, Strings.ErrorIsInvalid, nameof(webBrowser)));
			}

			try {
				var result = await webBrowser.UrlGetToJsonObject<JToken>(new Uri("https://httpbin.org/ip")).ConfigureAwait(false);
				string origin = result?.Content?.Value<string>("origin") ?? "";

				if (!string.IsNullOrWhiteSpace(origin)) {
					return formatBotResponse(origin);
				}
			}
			catch (Exception e) when (e is JsonException or IOException) {
				return formatBotResponse(string.Format(CultureInfo.CurrentCulture, Strings.ErrorIsInvalid, e.Message));
			}
		}

		if (args is { Length: > 0 } && (args[0]?.ToUpperInvariant() == "FREEGAMES")) {
			int collected = await CollectGames(CancellationTS.Value.Token).ConfigureAwait(false);

			return formatBotResponse($"Collected a total of {collected} free game(s)");
		}

		return null;
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
			await ctx.SaveToFileSystem().ConfigureAwait(false);
			ctx.Dispose();
		}

		if ((Bots.Count == 0)) {
			ResetTimer();
		}
	}

	private async Task RegisterBot(Bot bot) {
		Bots.Add(bot);

		if (Timer is null) {
			var delay = TimeSpan.FromMilliseconds(GlobalDatabase?.LoadFromJsonStorage($"{Name}.timer")?.ToObject<double>() ?? TimeSpan.FromMinutes(30).TotalMilliseconds);
			ResetTimer(new Timer(CollectGamesOnClock));
			Timer?.Change(TimeSpan.FromSeconds(30), delay);
		}

		if (!BotContexts.TryGetValue(bot.BotName, out var ctx)) {
			lock (BotContexts) {
				if (!BotContexts.TryGetValue(bot.BotName, out ctx)) {
					ctx = BotContexts[bot.BotName] = new BotContext(bot);
				}
			}
		}

		await ctx.LoadFromFileSystem(CancellationTS.Value.Token).ConfigureAwait(false);
	}

	public async Task OnBotLoggedOn(Bot bot) => await RegisterBot(bot).ConfigureAwait(false);

	private async void CollectGamesOnClock(object? source) {
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(CollectGamesTimeout));

		Bot[] reorderedBots;

		lock (BotContexts) {
			TimeSpan orderByRunKeySelector(Bot bot) => BotContexts.TryGetValue(bot.BotName, out var ctx) ? ctx.RunElapsed : TimeSpan.MaxValue;
			int comparison(Bot x, Bot y) => orderByRunKeySelector(y).CompareTo(orderByRunKeySelector(x)); // sort in descending order
			reorderedBots = Bots.ToArray();
			Array.Sort(reorderedBots, comparison);
		}

		await CollectGames(reorderedBots, cts.Token).ConfigureAwait(false);
	}

	private Task<int> CollectGames(CancellationToken cancellationToken = default) => CollectGames(Bots, cancellationToken);

	private async Task<int> CollectGames(IEnumerable<Bot> bots, CancellationToken cancellationToken = default) {
		if (cancellationToken.IsCancellationRequested) {
			return 0;
		}

		if (!await SemaphoreSlim.WaitAsync(100, cancellationToken).ConfigureAwait(false)) {
			return 0;
		}

		int res = 0;

		try {
			var games = await RedditHelper.ListGames().ConfigureAwait(false);

			ArchiLogger.LogGenericInfo($"found {games.Count} free games on reddit", nameof(CollectGames));

			foreach (Bot bot in bots) {
				if (cancellationToken.IsCancellationRequested) {
					break;
				}

				if (!bot.IsConnectedAndLoggedOn) {
					continue;
				}

				if (bot.GamesToRedeemInBackgroundCount > 0) {
					continue;
				}

				bool save = false;
				BotContext context = BotContexts[bot.BotName];

				foreach ((string? identifier, bool freeToPlay, long time) in games) {
					if (freeToPlay) {
						continue;
					}

					if (identifier is null || !GameIdentifier.TryParse(identifier, out var gid)) {
						continue;
					}

					if (context.HasApp(gid)) {
						continue;
					}

					string? resp = await bot.Commands.Response(EAccess.Operator, $"ADDLICENSE {bot.BotName} {gid}").ConfigureAwait(false);
					bool success = false;

					if (!string.IsNullOrWhiteSpace(resp)) {
						ArchiLogger.LogGenericInfo($"{resp}", nameof(CollectGames));
						success = resp!.Contains("collected game", StringComparison.InvariantCultureIgnoreCase);
						success |= resp!.Contains("OK", StringComparison.InvariantCultureIgnoreCase);
					}

					if (success) {
						lock (context) {
							context.RegisterApp(gid);
						}

						save = true;
						Interlocked.Increment(ref res);
					}
					else if (Math.Abs(Utilities.GetUnixTime() - time) > TimeSpan.FromHours(24).TotalSeconds) {
						context.AppTickCount(gid, increment: true);
					}
				}

				if (save) {
					await context.SaveToFileSystem(cancellationToken).ConfigureAwait(false);
				}

				context.NewRun();
			}
		}
		catch (TaskCanceledException) { }
		finally {
			SemaphoreSlim.Release();
		}

		return res;
	}
}
#pragma warning restore CA1812 // ASF uses this class during runtime
