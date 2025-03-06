using System;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using ASFFreeGames.ASFExtensions.Games;
using Maxisoft.ASF;
using Maxisoft.ASF.AppLists;

namespace ASFFreeGames.ASFExtensions.Bot;

using Bot = ArchiSteamFarm.Steam.Bot;

using static ArchiSteamFarm.Localization.Strings;
internal sealed class BotContext : IDisposable {
	private const ulong TriesBeforeBlacklistingGameEntry = 5;

	public long RunElapsedMilli => Environment.TickCount64 - LastRunMilli;

	private readonly Dictionary<GameIdentifier, (ulong counter, DateTime date)> AppRegistrationContexts = new();
	private readonly TimeSpan BlacklistTimeout = TimeSpan.FromDays(1);
	private readonly string BotIdentifier;
	private readonly CompletedAppList CompletedApps = new();
	private long LastRunMilli;

	public BotContext(Bot bot) {
		BotIdentifier = bot.BotName;
		NewRun();
	}

	public void Dispose() => CompletedApps.Dispose();

	public ulong AppTickCount(in GameIdentifier gameIdentifier, bool increment = false) {
		ulong res = 0;

		DateTime? dateTime = null;

		if (AppRegistrationContexts.TryGetValue(gameIdentifier, out (ulong counter, DateTime date) tuple)) {
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

	public bool HasApp(in GameIdentifier gameIdentifier) {
		if (!gameIdentifier.Valid) {
			return false;
		}

		if (AppRegistrationContexts.TryGetValue(gameIdentifier, out (ulong counter, DateTime date) tuple) && (tuple.counter >= TriesBeforeBlacklistingGameEntry)) {
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

		Bot? bot = Bot.GetBot(BotIdentifier);

		return bot is not null && GetBotOwnedPackages(bot).ContainsKey(checked((uint) gameIdentifier.Id));
	}

	private static Dictionary<uint, byte> GetBotOwnedPackages(Bot bot) {
		try {
			// Try to access OwnedPackages first (new name)
			PropertyInfo? ownedPackagesProperty = typeof(Bot).GetProperty("OwnedPackages", BindingFlags.Instance | BindingFlags.Public);

			if ((ownedPackagesProperty != null) && (ownedPackagesProperty.PropertyType == typeof(Dictionary<uint, byte>))) {
				return (Dictionary<uint, byte>) ownedPackagesProperty.GetValue(bot)!;
			}

			// Fallback to OwnedPackageIDs (old name)
			PropertyInfo? ownedPackageIDsProperty = typeof(Bot).GetProperty("OwnedPackageIDs", BindingFlags.Instance | BindingFlags.Public);

			if ((ownedPackageIDsProperty != null) && (ownedPackageIDsProperty.PropertyType == typeof(Dictionary<uint, byte>))) {
				return (Dictionary<uint, byte>) ownedPackageIDsProperty.GetValue(bot)!;
			}

			// If both fail, log an error
			bot.ArchiLogger.LogGenericError("Error: property 'OwnedPackages' or 'OwnedPackageIDs' not found.");
		}
		catch (Exception e) {
			bot.ArchiLogger.LogGenericException(e);
		}

		return new Dictionary<uint, byte>();
	}

	public async Task LoadFromFileSystem(CancellationToken cancellationToken = default) {
		string filePath = CompletedAppFilePath();
		await CompletedApps.LoadFromFile(filePath, cancellationToken).ConfigureAwait(false);
	}

	public void NewRun() => LastRunMilli = Environment.TickCount64;

	public void RegisterApp(in GameIdentifier gameIdentifier) {
		if (!gameIdentifier.Valid || !CompletedApps.Add(in gameIdentifier) || !CompletedApps.Contains(in gameIdentifier)) {
			AppRegistrationContexts[gameIdentifier] = (long.MaxValue, DateTime.MaxValue - BlacklistTimeout);
		}
	}

	public bool RegisterInvalidApp(in GameIdentifier gameIdentifier) => CompletedApps.AddInvalid(in gameIdentifier);

	public async Task SaveToFileSystem(CancellationToken cancellationToken = default) {
		string filePath = CompletedAppFilePath();
		await CompletedApps.SaveToFile(filePath, cancellationToken).ConfigureAwait(false);
	}

	public bool ShouldHideErrorLogForApp(in GameIdentifier gameIdentifier) => (AppTickCount(in gameIdentifier) > 0) || CompletedApps.ContainsInvalid(in gameIdentifier);

	private string CompletedAppFilePath() {
		Bot? bot = Bot.GetBot(BotIdentifier);

		if (bot is null) {
			return string.Empty;
		}

		string file = bot.GetFilePath(Bot.EFileType.Config);

		string res = file.Replace(".json", CompletedAppList.FileExtension, StringComparison.InvariantCultureIgnoreCase);

		if (res == file) {
			throw new FormatException("unable to replace json ext");
		}

		return res;
	}
}

