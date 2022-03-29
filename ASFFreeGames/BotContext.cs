using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using BloomFilter;

namespace Maxisoft.ASF;

internal sealed class BotContext {
	private readonly Filter<string> CompletedApp = new(1 << 12);
	private readonly HashSet<string> InMemoryCompletedApp = new HashSet<string>();
	private readonly WeakReference<Bot> Bot;
	public BotContext(Bot bot) => Bot = new WeakReference<Bot>(bot);

	private string CompletedAppFilePath() {
		if (!Bot.TryGetTarget(out var bot)) {
			return string.Empty;
		}

		string file = bot.GetFilePath(ArchiSteamFarm.Steam.Bot.EFileType.Config);

		string res = file.Replace(".json", ".fgapp.bfilter", StringComparison.InvariantCultureIgnoreCase);

		if (res == file) {
			throw new FormatException("unable to replace json ext");
		}

		return res;
	}

	public void SaveApp(string query) {
		CompletedApp.Add(query);
		InMemoryCompletedApp.Add(query);
	}

	public bool HasApp(string query) {
		if (InMemoryCompletedApp.Contains(query)) {
			return true;
		}

		if (!CompletedApp.Contains(query)) {
			return false;
		}

		if (!Bot.TryGetTarget(out var bot)) {
			return false;
		}

		if (query is null) {
#pragma warning disable CA2201
			throw new NullReferenceException(nameof(query));
#pragma warning restore CA2201
		}

		(uint appid, string type) = GetAppInfo(query);

		if (appid == 0) {
			return false;
		}

		return bot.OwnedPackageIDs.ContainsKey(appid);
	}

	private static (uint appid, string type) GetAppInfo(string query) {
		uint gameID;
		string type;

		int index = query.IndexOf('/', StringComparison.Ordinal);

		if ((index > 0) && (query.Length > index + 1)) {
			if (!uint.TryParse(query[(index + 1)..], out gameID) || (gameID == 0)) {
				return (0, string.Empty);
			}

			type = query[..index];
		}
		else if (uint.TryParse(query, out gameID) && (gameID > 0)) {
			type = "SUB";
		}
		else {
			return (0, string.Empty);
		}

		return (gameID, type);
	}

	public async Task Save() {
		var data = CompletedApp.ToArray();

		var filePath = CompletedAppFilePath();

		if (string.IsNullOrWhiteSpace(filePath)) {
			return;
		}

		using var sourceStream = new FileStream(
			filePath,
			FileMode.Create, FileAccess.Write, FileShare.None,
			bufferSize: 4096, useAsync: true
		);

		await sourceStream.WriteAsync(data).ConfigureAwait(false);
	}

	public async Task Load() {
		var filePath = CompletedAppFilePath();

		if (string.IsNullOrWhiteSpace(filePath)) {
			return;
		}

		byte[] data;

		try {
			data = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
		}
		catch (FileNotFoundException) {
			return;
		}

		CompletedApp.Populate(data);
	}
}
