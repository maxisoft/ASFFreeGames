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

#pragma warning disable CA1812 // ASF uses this class during runtime
[UsedImplicitly]
[SuppressMessage("Design", "CA1001:Disposable fields")]
internal sealed class ASFFreeGamesPlugin : IASF, IBot, IBotConnection, IBotCommand2, IUpdateAware {
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

	private ASFFreeGamesOptions Options = new();

	private Timer? Timer;

	public ASFFreeGamesPlugin() {
		CommandDispatcher = new CommandDispatcher(Options);
		_context.Value = new PluginContext(Bots, BotContextRegistry, Options, LoggerFilter, new Lazy<CancellationToken>(() => CancellationTokenSourceLazy.Value.Token));
	}

	public async Task OnASFInit(IReadOnlyDictionary<string, JToken>? additionalConfigProperties = null) {
		ASFFreeGamesOptionsLoader.Bind(ref Options);
		Options.VerboseLog ??= GlobalDatabase?.LoadFromJsonStorage($"{Name}.Verbose")?.ToObject<bool?>() ?? Options.VerboseLog;
		await SaveOptions(CancellationToken).ConfigureAwait(false);
	}

	public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) => await CommandDispatcher.Execute(bot, message, args, steamID).ConfigureAwait(false);

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

	private async void CollectGamesOnClock(object? source) {
		// Calculate a random delay using GetRandomizedTimerDelay method
		TimeSpan delay = GetRandomizedTimerDelay();

		// Reset the timer with the new delay
		ResetTimer(() => new Timer(CollectGamesOnClock, source, delay, delay));

		if ((Bots.Count > 0) && (Context.Bots.Count != Bots.Count)) {
			Context = new PluginContext(Bots, BotContextRegistry, Options, LoggerFilter, new Lazy<CancellationToken>(() => CancellationTokenSourceLazy.Value.Token));
		}

		using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		cts.CancelAfter(TimeSpan.FromMilliseconds(CollectGamesTimeout));

		if (cts.IsCancellationRequested) {
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

	private static readonly RandomUtils.GaussianRandom Random = new();

	/// <summary>
	/// Calculates a random delay using a normal distribution with a mean of Options.RecheckInterval.TotalSeconds and a standard deviation of 7 minutes.
	/// </summary>
	/// <returns>The randomized delay.</returns>
	/// <seealso cref="GetRandomizedTimerDelay(double, double, double, double)"/>
	private TimeSpan GetRandomizedTimerDelay() => GetRandomizedTimerDelay(Options.RecheckInterval.TotalSeconds, 7 * 60 * RandomizeIntervalSwitch);

	/// <summary>
	/// Gets a value that indicates whether to randomize the collect interval or not.
	/// </summary>
	/// <value>
	/// A value of 1 if Options.RandomizeRecheckInterval is true or null, or a value of 0 otherwise.
	/// </value>
	/// <remarks>
	/// This property is used to multiply the standard deviation of the normal distribution used to generate the random delay in the GetRandomizedTimerDelay method. If this property returns 0, then the random delay will be equal to the mean value.
	/// </remarks>
	private int RandomizeIntervalSwitch => (Options.RandomizeRecheckInterval ?? true ? 1 : 0);

	/// <summary>
	/// Calculates a random delay using a normal distribution with a given mean and standard deviation.
	/// </summary>
	/// <param name="meanSeconds">The mean of the normal distribution in seconds.</param>
	/// <param name="stdSeconds">The standard deviation of the normal distribution in seconds.</param>
	/// <param name="minSeconds">The minimum value of the random delay in seconds. The default value is 11 minutes.</param>
	/// <param name="maxSeconds">The maximum value of the random delay in seconds. The default value is 1 hour.</param>
	/// <returns>The randomized delay.</returns>
	/// <remarks>
	/// The random number is clamped between the minSeconds and maxSeconds parameters.
	/// This method uses the NextGaussian method from the RandomUtils class to generate normally distributed random numbers.
	/// See [Random nextGaussian() method in Java with Examples] for more details on how to implement NextGaussian in C#.
	/// </remarks>
	private static TimeSpan GetRandomizedTimerDelay(double meanSeconds, double stdSeconds, double minSeconds = 11 * 60, double maxSeconds = 60 * 60) {
		double randomNumber;

		randomNumber = stdSeconds != 0 ? Random.NextGaussian(meanSeconds, stdSeconds) : meanSeconds;

		TimeSpan delay = TimeSpan.FromSeconds(randomNumber);

		// Convert delay to seconds
		double delaySeconds = delay.TotalSeconds;

		// Clamp the delay between minSeconds and maxSeconds in seconds
		delaySeconds = Math.Max(delaySeconds, minSeconds);
		delaySeconds = Math.Min(delaySeconds, maxSeconds);

		// Convert delay back to TimeSpan
		delay = TimeSpan.FromSeconds(delaySeconds);

		return delay;
	}

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
			ResetTimer();
		}

		Context.LoggerFilter.RemoveFilters(bot);
	}

	private void ResetTimer(Func<Timer?>? newTimerFactory = null) {
		Timer?.Dispose();
		Timer = null;

		if (newTimerFactory is not null) {
			Timer = newTimerFactory();
		}
	}

	private async Task SaveOptions(CancellationToken cancellationToken) {
		if (!cancellationToken.IsCancellationRequested) {
			const string cmd = $"FREEGAMES {FreeGamesCommand.SaveOptionsInternalCommandString}";
			await OnBotCommand(Bots.FirstOrDefault()!, EAccess.None, cmd, cmd.Split()).ConfigureAwait(false);
		}
	}

	private void StartTimerIfNeeded() {
		if (Timer is null) {
			TimeSpan delay = GetRandomizedTimerDelay();
			ResetTimer(() => new Timer(CollectGamesOnClock));
			Timer?.Change(GetRandomizedTimerDelay(30, 6 * RandomizeIntervalSwitch, 1, 5 * 60), delay);
		}
	}

	~ASFFreeGamesPlugin() => ResetTimer();
}

#pragma warning restore CA1812 // ASF uses this class during runtime
