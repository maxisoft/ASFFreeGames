using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using ASFFreeGames.ASFExtensions.Bot;
using ASFFreeGames.ASFExtensions.Games;
using ASFFreeGames.Configurations;
using Maxisoft.ASF;
using Maxisoft.ASF.ASFExtensions;
using Maxisoft.ASF.Configurations;
using Maxisoft.ASF.FreeGames.Strategies;
using Maxisoft.ASF.HttpClientSimple;
using Maxisoft.ASF.Reddit;
using Maxisoft.ASF.Utils;
using SteamKit2;

namespace ASFFreeGames.Commands {
	// Implement the IBotCommand interface
	internal sealed class FreeGamesCommand(ASFFreeGamesOptions options) : IBotCommand, IDisposable {
		public void Dispose() {
			Strategy.Dispose();

			if (HttpFactory.IsValueCreated) {
				HttpFactory.Value.Dispose();
			}

			SemaphoreSlim?.Dispose();
		}

		internal const string SaveOptionsInternalCommandString = "_SAVEOPTIONS";
		internal const string CollectInternalCommandString = "_COLLECT";

		private static PluginContext Context => ASFFreeGamesPlugin.Context;

		// Declare a private field for the plugin options instance
		private ASFFreeGamesOptions Options = options ?? throw new ArgumentNullException(nameof(options));

		private readonly Lazy<SimpleHttpClientFactory> HttpFactory = new(() => new SimpleHttpClientFactory(options));

		public IListFreeGamesStrategy Strategy { get; internal set; } = new ListFreeGamesMainStrategy();
		public EListFreeGamesStrategy PreviousSucessfulStrategy { get; private set; } = EListFreeGamesStrategy.Reddit | EListFreeGamesStrategy.Redlib;

		// Define a constructor that takes an plugin options instance as a parameter

		/// <inheritdoc />
		/// <summary>
		/// Executes the FREEGAMES command, which allows the user to collect free games from a Reddit list or set or reload the plugin options.
		/// </summary>
		/// <param name="bot">The bot instance that received the command.</param>
		/// <param name="message">The message that contains the command.</param>
		/// <param name="args">The arguments of the command.</param>
		/// <param name="steamID">The SteamID of the user who sent the command.</param>
		/// <param name="cancellationToken"></param>
		/// <returns>A string response that indicates the result of the command execution.</returns>
		public async Task<string?> Execute(Bot? bot, string message, string[] args, ulong steamID = 0, CancellationToken cancellationToken = default) {
			if (args.Length >= 2) {
				switch (args[1].ToUpperInvariant()) {
					case "SET":
						return await HandleSetCommand(bot, args, cancellationToken).ConfigureAwait(false);
					case "RELOAD":
						return await HandleReloadCommand(bot).ConfigureAwait(false);
					case SaveOptionsInternalCommandString:
						return await HandleInternalSaveOptionsCommand(bot, cancellationToken).ConfigureAwait(false);
					case CollectInternalCommandString:
						return await HandleInternalCollectCommand(bot, args, cancellationToken).ConfigureAwait(false);
					case "SHOWBLACKLIST":
						if (Options.Blacklist.Count == 0) {
							return FormatBotResponse(bot, "Blacklist is empty");
						} else {
							string blacklistItems = string.Join(", ", Options.Blacklist);
							return FormatBotResponse(bot, $"Current blacklist: {blacklistItems}");
						}
				}
			}

			return await HandleCollectCommand(bot).ConfigureAwait(false);
		}

		private static string FormatBotResponse(Bot? bot, string resp) => IBotCommand.FormatBotResponse(bot, resp);

		private async Task<string?> HandleSetCommand(Bot? bot, string[] args, CancellationToken cancellationToken) {
			using CancellationTokenSource cts = CreateLinkedTokenSource(cancellationToken);
			cancellationToken = cts.Token;

			if (args.Length >= 3) {
				switch (args[2].ToUpperInvariant()) {
					case "VERBOSE":
						Options.VerboseLog = true;
						await SaveOptions(cancellationToken).ConfigureAwait(false);

						return FormatBotResponse(bot, "Verbosity on");
					case "NOVERBOSE":
						Options.VerboseLog = false;
						await SaveOptions(cancellationToken).ConfigureAwait(false);

						return FormatBotResponse(bot, "Verbosity off");
					case "F2P":
					case "FREETOPLAY":
					case "NOSKIPFREETOPLAY":
						Options.SkipFreeToPlay = false;
						await SaveOptions(cancellationToken).ConfigureAwait(false);

						return FormatBotResponse(bot, $"{ASFFreeGamesPlugin.StaticName} is going to collect f2p games");
					case "NOF2P":
					case "NOFREETOPLAY":
					case "SKIPFREETOPLAY":
						Options.SkipFreeToPlay = true;
						await SaveOptions(cancellationToken).ConfigureAwait(false);

						return FormatBotResponse(bot, $"{ASFFreeGamesPlugin.StaticName} is now skipping f2p games");
					case "DLC":
					case "NOSKIPDLC":
						Options.SkipDLC = false;
						await SaveOptions(cancellationToken).ConfigureAwait(false);

						return FormatBotResponse(bot, $"{ASFFreeGamesPlugin.StaticName} is going to collect dlc");
					case "NODLC":
					case "SKIPDLC":
						Options.SkipDLC = true;
						await SaveOptions(cancellationToken).ConfigureAwait(false);

						return FormatBotResponse(bot, $"{ASFFreeGamesPlugin.StaticName} is now skipping dlc");
					case "CLEARBLACKLIST":
						Options.ClearBlacklist();
						await SaveOptions(cancellationToken).ConfigureAwait(false);

						return FormatBotResponse(bot, $"{ASFFreeGamesPlugin.StaticName} blacklist has been cleared");
					case "REMOVEBLACKLIST":
						if (args.Length >= 4) {
							string identifier = args[3];
							if (GameIdentifier.TryParse(identifier, out GameIdentifier gid)) {
								bool removed = Options.RemoveFromBlacklist(in gid);
								await SaveOptions(cancellationToken).ConfigureAwait(false);

								if (removed) {
									return FormatBotResponse(bot, $"{ASFFreeGamesPlugin.StaticName} removed {gid} from blacklist");
								} else {
									return FormatBotResponse(bot, $"{ASFFreeGamesPlugin.StaticName} could not find {gid} in blacklist");
								}
							} else {
								return FormatBotResponse(bot, $"Invalid game identifier format: {identifier}");
							}
						} else {
							return FormatBotResponse(bot, "Please provide a game identifier to remove from blacklist");
						}
					case "SHOWBLACKLIST":
						if (Options.Blacklist.Count == 0) {
							return FormatBotResponse(bot, "Blacklist is empty");
						} else {
							string blacklistItems = string.Join(", ", Options.Blacklist);
							return FormatBotResponse(bot, $"Current blacklist: {blacklistItems}");
						}
					default:
						return FormatBotResponse(bot, $"Unknown \"{args[2]}\" variable to set");
				}
			}

			return null;
		}

		/// <summary>
		/// Creates a linked cancellation token source from the given cancellation token and the Context cancellation token.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token to link.</param>
		/// <returns>A CancellationTokenSource that is linked to both tokens.</returns>
		private static CancellationTokenSource CreateLinkedTokenSource(CancellationToken cancellationToken) => Context.Valid ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Context.CancellationToken) : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		private Task<string?> HandleReloadCommand(Bot? bot) {
			ASFFreeGamesOptionsLoader.Bind(ref Options);

			return Task.FromResult(FormatBotResponse(bot, $"Reloaded {ASFFreeGamesPlugin.StaticName} options"))!;
		}

		private async Task<string?> HandleCollectCommand(Bot? bot) {
			int collected = await CollectGames(bot is not null ? [bot] : Context.Bots.ToArray(), ECollectGameRequestSource.RequestedByUser, Context.CancellationToken).ConfigureAwait(false);

			return FormatBotResponse(bot, $"Collected a total of {collected} free game(s)");
		}

		private async ValueTask<string?> HandleInternalSaveOptionsCommand(Bot? bot, CancellationToken cancellationToken) {
			await SaveOptions(cancellationToken).ConfigureAwait(false);

			return null;
		}

		private async ValueTask<string?> HandleInternalCollectCommand(Bot? bot, string[] args, CancellationToken cancellationToken) {
			Dictionary<string, Bot> botMap = Context.Bots.ToDictionary(static b => b.BotName.Trim(), static b => b, StringComparer.InvariantCultureIgnoreCase);

			List<Bot> bots = [];

			for (int i = 2; i < args.Length; i++) {
				string botName = args[i].Trim();

				// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
				if (botMap.TryGetValue(botName, out Bot? savedBot) && savedBot is not null) {
					bots.Add(savedBot);
				}
			}

			if (bots.Count == 0) {
				if (bot is null) {
					return null;
				}

				bots = [bot];
			}

			int collected = await CollectGames(bots, ECollectGameRequestSource.Scheduled, cancellationToken).ConfigureAwait(false);

			return FormatBotResponse(bot, $"Collected a total of {collected} free game(s)" + (bots.Count > 1 ? $" on {bots.Count} bots" : $" on {bots.FirstOrDefault()?.BotName}"));
		}

		private async Task SaveOptions(CancellationToken cancellationToken) {
			using CancellationTokenSource cts = CreateLinkedTokenSource(cancellationToken);
			cancellationToken = cts.Token;
			cts.CancelAfter(10_000);
			await ASFFreeGamesOptionsLoader.Save(Options, cancellationToken).ConfigureAwait(false);
		}

		private SemaphoreSlim? SemaphoreSlim;
		private readonly object LockObject = new();
		private readonly HashSet<GameIdentifier> PreviouslySeenAppIds = new();
		private static LoggerFilter LoggerFilter => Context.LoggerFilter;
		private const int DayInSeconds = 24 * 60 * 60;
		private static readonly Lazy<Regex> InvalidAppPurchaseRegex = new(BuildInvalidAppPurchaseRegex);

		private static readonly EPurchaseResultDetail[] InvalidAppPurchaseCodes = { EPurchaseResultDetail.AlreadyPurchased, EPurchaseResultDetail.RegionNotSupported, EPurchaseResultDetail.InvalidPackage, EPurchaseResultDetail.DoesNotOwnRequiredApp };

		// ReSharper disable once RedundantDefaultMemberInitializer
#pragma warning disable CA1805
		internal bool VerboseLog =>
#if DEBUG
			Options.VerboseLog ?? true
#else
		Options.VerboseLog ?? false
#endif
		;
#pragma warning restore CA1805

		private async Task<int> CollectGames(IEnumerable<Bot> bots, ECollectGameRequestSource requestSource, CancellationToken cancellationToken = default) {
			using CancellationTokenSource cts = CreateLinkedTokenSource(cancellationToken);
			cancellationToken = cts.Token;

			if (cancellationToken.IsCancellationRequested) {
				return 0;
			}

			SemaphoreSlim? semaphore = SemaphoreSlim;

			if (semaphore is null) {
				lock (LockObject) {
					SemaphoreSlim ??= new SemaphoreSlim(1, 1);
					semaphore = SemaphoreSlim;
				}
			}

			if (!await semaphore.WaitAsync(100, cancellationToken).ConfigureAwait(false)) {
				return 0;
			}

			int res = 0;

			try {
				IReadOnlyCollection<RedditGameEntry> games;

				ListFreeGamesContext strategyContext = new(Options, new Lazy<SimpleHttpClient>(() => HttpFactory.Value.CreateGeneric())) {
					Strategy = Strategy,
					HttpClientFactory = HttpFactory.Value,
					PreviousSucessfulStrategy = PreviousSucessfulStrategy
				};

				// Cache of known invalid packages to avoid repeated failed attempts within the same collection run
				HashSet<string> knownInvalidPackages = new();

				try {
#pragma warning disable CA2000
					games = await Strategy.GetGames(strategyContext, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2000
				}
				catch (Exception e) when (e is InvalidOperationException or JsonException or IOException or RedditServerException) {
					if (Options.VerboseLog ?? false) {
						ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericException(e);
					}
					else {
						ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericError($"Unable to get and load json {e.GetType().Name}: {e.Message}");
					}

					return 0;
				}
				finally {
					PreviousSucessfulStrategy = strategyContext.PreviousSucessfulStrategy;

					if (Options.VerboseLog ?? false) {
						ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericInfo($"PreviousSucessfulStrategy = {PreviousSucessfulStrategy}");
					}
				}

#pragma warning disable CA1308
				string remote = strategyContext.PreviousSucessfulStrategy.ToString().ToLowerInvariant();
#pragma warning restore CA1308
				LogNewGameCount(games, remote, VerboseLog || requestSource is ECollectGameRequestSource.RequestedByUser);

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

					if (Options.IsBlacklisted(bot)) {
						continue;
					}

					bool save = false;
					BotContext? context = Context.BotContexts.GetBotContext(bot);

					if (context is null) {
						continue;
					}

					foreach ((string identifier, long time, bool freeToPlay, bool dlc) in games) {
						if (freeToPlay && Options.SkipFreeToPlay is true) {
							continue;
						}

						if (dlc && Options.SkipDLC is true) {
							continue;
						}

						if (string.IsNullOrWhiteSpace(identifier) || !GameIdentifier.TryParse(identifier, out GameIdentifier gid)) {
							continue;
						}

						if (context.HasApp(in gid)) {
							continue;
						}

						if (Options.IsBlacklisted(in gid)) {
							continue;
						}

						// Skip packages that have already failed in this collection run
						if (knownInvalidPackages.Contains(gid.ToString())) {
							if (VerboseLog) {
								bot.ArchiLogger.LogGenericDebug($"Skipping previously failed package in this run: {gid}", nameof(CollectGames));
							}
							continue;
						}

						string? resp;

						string cmd = $"ADDLICENSE {bot.BotName} {gid}";

						if (VerboseLog) {
							bot.ArchiLogger.LogGenericDebug($"Trying to perform command \"{cmd}\"", nameof(CollectGames));
						}

						int retryAttempts = 0;
						int maxRetries = Options.MaxRetryAttempts ?? 1;
						bool isTransientError = false;

						do {
							if (retryAttempts > 0) {
								// Add delay before retry
								int retryDelay = Options.RetryDelayMilliseconds ?? 2000;
								await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);

								if (VerboseLog || requestSource is ECollectGameRequestSource.RequestedByUser) {
									bot.ArchiLogger.LogGenericInfo($"[FreeGames] Retry attempt {retryAttempts} for {gid}", nameof(CollectGames));
								}
							}

							using (LoggerFilter.DisableLoggingForAddLicenseCommonErrors(_ => !VerboseLog && (requestSource is not ECollectGameRequestSource.RequestedByUser) && context.ShouldHideErrorLogForApp(in gid), bot)) {
								resp = await bot.Commands.Response(EAccess.Operator, cmd).ConfigureAwait(false);
							}

							bool success = false;

							if (!string.IsNullOrWhiteSpace(resp)) {
								success = resp!.Contains("collected game", StringComparison.InvariantCultureIgnoreCase);
								success |= resp!.Contains("OK", StringComparison.InvariantCultureIgnoreCase);

								// Check if this is a transient error that should be retried
								isTransientError = !success &&
									(resp.Contains("timeout", StringComparison.InvariantCultureIgnoreCase) ||
									 resp.Contains("connection error", StringComparison.InvariantCultureIgnoreCase) ||
									 resp.Contains("service unavailable", StringComparison.InvariantCultureIgnoreCase));

								// Don't retry if we got a clear "Forbidden" or other definitive error
								if (resp.Contains("Forbidden", StringComparison.InvariantCultureIgnoreCase) ||
									resp.Contains("RateLimited", StringComparison.InvariantCultureIgnoreCase) ||
									resp.Contains("no eligible accounts", StringComparison.InvariantCultureIgnoreCase)) {
									isTransientError = false;
								}

								// Log the result regardless of success if it's verbose or user-requested
								if (success || (!isTransientError && (VerboseLog || requestSource is ECollectGameRequestSource.RequestedByUser || !context.ShouldHideErrorLogForApp(in gid)))) {
									string statusMessage;
									if (success) {
										statusMessage = "Success";
									} else if (resp.Contains("Forbidden", StringComparison.InvariantCultureIgnoreCase)) {
										statusMessage = "AccessDenied/InvalidPackage";
									} else if (resp.Contains("RateLimited", StringComparison.InvariantCultureIgnoreCase)) {
										statusMessage = "RateLimited";
									} else if (resp.Contains("timeout", StringComparison.InvariantCultureIgnoreCase)) {
										statusMessage = "Timeout";
									} else if (resp.Contains("no eligible accounts", StringComparison.InvariantCultureIgnoreCase)) {
										statusMessage = "NoEligibleAccounts";
									} else {
										statusMessage = "Failed";
									}

									bot.ArchiLogger.LogGenericInfo($"[FreeGames] <{bot.BotName}> ID: {gid} | Status: {statusMessage}{(isTransientError && retryAttempts < maxRetries ? " (Will retry)" : "")}", nameof(CollectGames));
								}
							}

							// If request was successful or this is not a transient error, break the loop
							if (success || !isTransientError) {

								if (success) {
									lock (context) {
										context.RegisterApp(in gid);
									}

									save = true;
									res++;
								}
								else {
									// Add the game to the processed list even if it failed with Forbidden to avoid retrying
									if (resp?.Contains("Forbidden", StringComparison.InvariantCultureIgnoreCase) ?? false) {
										lock (context) {
											// Register the app as attempted but failed due to access restrictions
											context.RegisterApp(in gid);
										}
										save = true;

										// Add to the known invalid packages for this collection run
										knownInvalidPackages.Add(gid.ToString());

										// Optionally blacklist this game ID if auto-blacklisting is enabled
										if (Options.AutoBlacklistForbiddenPackages ?? true) {
											Options.AddToBlacklist(in gid);

											if (VerboseLog || requestSource is ECollectGameRequestSource.RequestedByUser) {
												bot.ArchiLogger.LogGenericInfo($"[FreeGames] Adding {gid} to blacklist due to Forbidden response", nameof(CollectGames));
											}

											// Save the updated options to persist the blacklist
											_ = Task.Run(async () => {
												try {
													await SaveOptions(cancellationToken).ConfigureAwait(false);
												} catch (Exception ex) {
													if (VerboseLog || requestSource is ECollectGameRequestSource.RequestedByUser) {
														bot.ArchiLogger.LogGenericWarning($"Failed to save options after blacklisting: {ex.Message}", nameof(CollectGames));
													}
												}
											});
										}

										if (VerboseLog || requestSource is ECollectGameRequestSource.RequestedByUser) {
											bot.ArchiLogger.LogGenericWarning($"[FreeGames] Access denied for {gid}. The package may no longer be available or there are restrictions.", nameof(CollectGames));
										}
									}

									if ((requestSource != ECollectGameRequestSource.RequestedByUser) && (resp?.Contains("RateLimited", StringComparison.InvariantCultureIgnoreCase) ?? false)) {
										if (VerboseLog) {
											bot.ArchiLogger.LogGenericWarning("[FreeGames] Rate limit reached ! Skipping remaining games...", nameof(CollectGames));
										}

										break;
									}
								}

								// Check if we need to update app tick counts or register invalid apps
								if ((!success || isTransientError) && resp != null) {
									if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - time > DayInSeconds) {
										lock (context) {
											context.AppTickCount(in gid, increment: true);
										}
									}

									if (InvalidAppPurchaseRegex.Value.IsMatch(resp)) {
										save |= context.RegisterInvalidApp(in gid);
									}
								}

								break;
							}

							retryAttempts++;
						} while (isTransientError && retryAttempts <= maxRetries);

						// Add a delay between requests to avoid hitting rate limits
						int delay = Options.DelayBetweenRequests ?? 500;
						if (delay > 0) {
							await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
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
				semaphore.Release();
			}

			return res;
		}

		private void LogNewGameCount(IReadOnlyCollection<RedditGameEntry> games, string remote, bool logZero = false) {
			int totalAppIdCounter = PreviouslySeenAppIds.Count;
			int newGameCounter = 0;

			foreach (RedditGameEntry entry in games) {
				if (GameIdentifier.TryParse(entry.Identifier, out GameIdentifier identifier) && PreviouslySeenAppIds.Add(identifier)) {
					newGameCounter++;
				}
			}

			if ((totalAppIdCounter == 0) && (games.Count > 0)) {
				ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericInfo($"[FreeGames] found potentially {games.Count} free games on {remote}", nameof(CollectGames));
			}
			else if (newGameCounter > 0) {
				ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericInfo($"[FreeGames] found {newGameCounter} fresh free game(s) on {remote}", nameof(CollectGames));
			}
			else if ((newGameCounter == 0) && logZero) {
				ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericInfo($"[FreeGames] found 0 new game out of {games.Count} free games on {remote}", nameof(CollectGames));
			}
		}

		private static Regex BuildInvalidAppPurchaseRegex() {
			StringBuilder stringBuilder = new("^.*?(?:");

			foreach (EPurchaseResultDetail code in InvalidAppPurchaseCodes) {
				stringBuilder.Append("(?:");
				ReadOnlySpan<char> codeString = code.ToString().Replace(nameof(EPurchaseResultDetail), @"\w*?", StringComparison.InvariantCultureIgnoreCase);

				while ((codeString.Length > 0) && (codeString[0] == '.')) {
					codeString = codeString[1..];
				}

				if (codeString.Length <= 1) {
					continue;
				}

				stringBuilder.Append(codeString[0]);

				foreach (char c in codeString[1..]) {
					if (char.IsUpper(c)) {
						stringBuilder.Append(@"(?>\s*)");
					}

					stringBuilder.Append(c);
				}

				stringBuilder.Append(")|");
			}

			while ((stringBuilder.Length > 0) && (stringBuilder[^1] == '|')) {
				stringBuilder.Length -= 1;
			}

			stringBuilder.Append(").*?$");

			return new Regex(stringBuilder.ToString(), RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
		}
	}
}
