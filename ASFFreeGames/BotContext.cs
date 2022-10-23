using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using BloomFilter;

namespace Maxisoft.ASF;

internal sealed class BotContext : IDisposable {
	private const ulong TriesBeforeBlacklistingGameEntry = 5;

	private readonly Dictionary<GameIdentifier, (ulong counter, DateTime date)> AppRegistrationContexts = new();
	private readonly WeakReference<Bot> Bot;
	private readonly TimeSpan BlacklistTimeout = TimeSpan.FromDays(1);
	private readonly CompletedAppList CompletedApps = new();
	private long LastRunMilli;

	public BotContext(Bot bot) {
		Bot = new WeakReference<Bot>(bot);
		NewRun();
	}

	private string CompletedAppFilePath() {
		if (!Bot.TryGetTarget(out var bot)) {
			return string.Empty;
		}

		string file = bot.GetFilePath(ArchiSteamFarm.Steam.Bot.EFileType.Config);

		string res = file.Replace(".json", CompletedAppList.FileExtension, StringComparison.InvariantCultureIgnoreCase);

		if (res == file) {
			throw new FormatException("unable to replace json ext");
		}

		return res;
	}

	public void RegisterApp(in GameIdentifier gameIdentifier) {
		if (!gameIdentifier.Valid || !CompletedApps.Add(in gameIdentifier) || !CompletedApps.Contains(in gameIdentifier)) {
			AppRegistrationContexts[gameIdentifier] = (long.MaxValue, DateTime.MaxValue - BlacklistTimeout);
		}
	}

	public void RegisterInvalidApp(in GameIdentifier gameIdentifier) => CompletedApps.AddInvalid(in gameIdentifier);

	public bool HasApp(in GameIdentifier gameIdentifier) {
		if (!gameIdentifier.Valid) {
			return false;
		}

		if (AppRegistrationContexts.TryGetValue(gameIdentifier, out var tuple) && (tuple.counter >= TriesBeforeBlacklistingGameEntry)) {
			if (DateTime.UtcNow - tuple.date > BlacklistTimeout) {
				AppRegistrationContexts.Remove(gameIdentifier);
			}
			else {
				return true;
			}
		}

		if (CompletedApps.Contains(in gameIdentifier)) {
			return true;
		}

		if (!Bot.TryGetTarget(out var bot)) {
			return false;
		}

		return bot.OwnedPackageIDs.ContainsKey(checked((uint) gameIdentifier.Id));
	}

	public bool ShouldHideErrorLogForApp(in GameIdentifier gameIdentifier) => (AppTickCount(in gameIdentifier) > 0) || CompletedApps.ContainsInvalid(in gameIdentifier);

	public ulong AppTickCount(in GameIdentifier gameIdentifier, bool increment = false) {
		ulong res = 0;

		DateTime? dateTime = null;

		if (AppRegistrationContexts.TryGetValue(gameIdentifier, out var tuple)) {
			if (DateTime.UtcNow - tuple.date > BlacklistTimeout) {
				AppRegistrationContexts.Remove(gameIdentifier);
			}
			else {
				res = tuple.counter;
				dateTime = tuple.date;
			}
		}

		if (increment) {
			checked {
				res += 1;
			}

			AppRegistrationContexts[gameIdentifier] = (res, dateTime ?? DateTime.UtcNow);
		}

		return res;
	}

	public async Task LoadFromFileSystem(CancellationToken cancellationToken = default) {
		string filePath = CompletedAppFilePath();
		await CompletedApps.LoadFromFile(filePath, cancellationToken).ConfigureAwait(false);
	}

	public async Task SaveToFileSystem(CancellationToken cancellationToken = default) {
		string filePath = CompletedAppFilePath();
		await CompletedApps.SaveToFile(filePath, cancellationToken).ConfigureAwait(false);
	}

	public void Dispose() => CompletedApps.Dispose();

	public long RunElapsedMilli => Environment.TickCount64 - LastRunMilli;

	public void NewRun() => LastRunMilli = Environment.TickCount64;
}
