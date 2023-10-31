using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Collections;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ASFFreeGames.Commands;
using JetBrains.Annotations;
using Maxisoft.ASF.Configurations;
using Newtonsoft.Json.Linq;
using SteamKit2;
using static ArchiSteamFarm.Core.ASF;

namespace Maxisoft.ASF;

internal interface IASFFreeGamesPlugin {
	internal Version Version { get; }
	internal ASFFreeGamesOptions Options { get; }

	internal void CollectGamesOnClock(object? source);
}

#pragma warning disable CA1812 // ASF uses this class during runtime
[UsedImplicitly]
[SuppressMessage("Design", "CA1001:Disposable fields")]
internal sealed class ASFFreeGamesPlugin : IASF, IBot, IBotConnection, IBotCommand2, IUpdateAware, IASFFreeGamesPlugin {
	internal const string StaticName = nameof(ASFFreeGamesPlugin);
	private const int CollectGamesTimeout = 3 * 60 * 1000;

	internal static PluginContext Context {
		get => _context.Value;
		private set => _context.Value = value;
	}

	// ReSharper disable once InconsistentNaming
	private static readonly AsyncLocal<PluginContext> _context = new();
	private static CancellationToken CancellationToken => Context.CancellationToken;

	public string Name => StaticName;
	public Version Version => typeof(ASFFreeGamesPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	private readonly ConcurrentHashSet<Bot> Bots = new(new BotEqualityComparer());
	private readonly Lazy<CancellationTokenSource> CancellationTokenSourceLazy = new(static () => new CancellationTokenSource());
	private readonly CommandDispatcher CommandDispatcher;

	private readonly LoggerFilter LoggerFilter = new();

	private bool VerboseLog => Options.VerboseLog ?? true;
	private readonly ContextRegistry BotContextRegistry = new();

	public ASFFreeGamesOptions Options => OptionsField;
	private ASFFreeGamesOptions OptionsField = new();

	private readonly ICollectIntervalManager CollectIntervalManager;

	public ASFFreeGamesPlugin() {
		CommandDispatcher = new CommandDispatcher(Options);
		CollectIntervalManager = new CollectIntervalManager(this);
		_context.Value = new PluginContext(Bots, BotContextRegistry, Options, LoggerFilter, new Lazy<CancellationToken>(() => CancellationTokenSourceLazy.Value.Token));
	}

	public async Task OnASFInit(IReadOnlyDictionary<string, JToken>? additionalConfigProperties = null) {
		ASFFreeGamesOptionsLoader.Bind(ref OptionsField);
		Options.VerboseLog ??= GlobalDatabase?.LoadFromJsonStorage($"{Name}.Verbose")?.ToObject<bool?>() ?? Options.VerboseLog;
		await SaveOptions(CancellationToken).ConfigureAwait(false);
	}

	public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) {
		if (!Context.Valid) {
			CreateContext();
		}

		return await CommandDispatcher.Execute(bot, message, args, steamID).ConfigureAwait(false);
	}

	public async Task OnBotDestroy(Bot bot) => await RemoveBot(bot).ConfigureAwait(false);

	public async Task OnBotDisconnected(Bot bot, EResult reason) => await RemoveBot(bot).ConfigureAwait(false);

	public Task OnBotInit(Bot bot) => Task.CompletedTask;

	public async Task OnBotLoggedOn(Bot bot) => await RegisterBot(bot).ConfigureAwait(false);

	public Task OnLoaded() {
		if (VerboseLog) {
			ArchiLogger.LogGenericInfo($"Loaded {Name}");
		}

		return Task.CompletedTask;
	}

	public async Task OnUpdateFinished(Version currentVersion, Version newVersion) => await SaveOptions(Context.CancellationToken).ConfigureAwait(false);

	public Task OnUpdateProceeding(Version currentVersion, Version newVersion) => Task.CompletedTask;

	public async void CollectGamesOnClock(object? source) {
		CollectIntervalManager.RandomlyChangeCollectInterval(source);

		if (!Context.Valid || ((Bots.Count > 0) && (Context.Bots.Count != Bots.Count))) {
			CreateContext();
		}

		using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		cts.CancelAfter(TimeSpan.FromMilliseconds(CollectGamesTimeout));

		if (cts.IsCancellationRequested || !Context.Valid) {
			return;
		}

		Bot[] reorderedBots;
		IContextRegistry botContexts = Context.BotContexts;

		lock (botContexts) {
			long orderByRunKeySelector(Bot bot) => botContexts.GetBotContext(bot)?.RunElapsedMilli ?? long.MaxValue;
			int comparison(Bot x, Bot y) => orderByRunKeySelector(y).CompareTo(orderByRunKeySelector(x)); // sort in descending order
			reorderedBots = Bots.ToArray();
			Array.Sort(reorderedBots, comparison);
		}

		if (!cts.IsCancellationRequested) {
			string cmd = $"FREEGAMES {FreeGamesCommand.CollectInternalCommandString} " + string.Join(' ', reorderedBots.Select(static bot => bot.BotName));
			await OnBotCommand(null!, EAccess.None, cmd, cmd.Split()).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a new PluginContext instance and assigns it to the Context property.
	/// </summary>
	private void CreateContext() => Context = new PluginContext(Bots, BotContextRegistry, Options, LoggerFilter, new Lazy<CancellationToken>(() => CancellationTokenSourceLazy.Value.Token), true);

	private async Task RegisterBot(Bot bot) {
		Bots.Add(bot);

		StartTimerIfNeeded();

		await BotContextRegistry.SaveBotContext(bot, new BotContext(bot), CancellationToken).ConfigureAwait(false);
		BotContext? ctx = BotContextRegistry.GetBotContext(bot);

		if (ctx is not null) {
			await ctx.LoadFromFileSystem(CancellationToken).ConfigureAwait(false);
		}
	}

	private async Task RemoveBot(Bot bot) {
		Bots.Remove(bot);

		BotContext? botContext = BotContextRegistry.GetBotContext(bot);

		if (botContext is not null) {
			try {
				await botContext.SaveToFileSystem(CancellationToken).ConfigureAwait(false);
			}
			finally {
				await BotContextRegistry.RemoveBotContext(bot).ConfigureAwait(false);
				botContext.Dispose();
			}
		}

		if (Bots.Count == 0) {
			CollectIntervalManager.StopTimer();
		}

		LoggerFilter.RemoveFilters(bot);
	}

	private async Task SaveOptions(CancellationToken cancellationToken) {
		if (!cancellationToken.IsCancellationRequested) {
			const string cmd = $"FREEGAMES {FreeGamesCommand.SaveOptionsInternalCommandString}";
			await OnBotCommand(Bots.FirstOrDefault()!, EAccess.None, cmd, cmd.Split()).ConfigureAwait(false);
		}
	}

	private void StartTimerIfNeeded() => CollectIntervalManager.StartTimerIfNeeded();

	~ASFFreeGamesPlugin() => CollectIntervalManager.Dispose();
}

#pragma warning restore CA1812 // ASF uses this class during runtime
