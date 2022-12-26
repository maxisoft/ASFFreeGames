using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm;
using Microsoft.Extensions.Configuration;

namespace Maxisoft.ASF.Configurations;

public static class ASFFreeGamesOptionsLoader {
	public static void Bind(ref ASFFreeGamesOptions options) {
		options ??= new ASFFreeGamesOptions();
		Semaphore.Wait();

		try {
			IConfigurationRoot configurationRoot = CreateConfigurationRoot();

			IEnumerable<string> blacklist = configurationRoot.GetValue("Blacklist", options.Blacklist) ?? options.Blacklist;
			options.Blacklist = new HashSet<string>(blacklist, StringComparer.InvariantCultureIgnoreCase);

			options.VerboseLog = configurationRoot.GetValue("VerboseLog", options.VerboseLog);
			options.RecheckIntervalMs = configurationRoot.GetValue("RecheckIntervalMs", options.RecheckIntervalMs);
			options.SkipFreeToPlay = configurationRoot.GetValue("SkipFreeToPlay", options.SkipFreeToPlay);
			options.SkipDLC = configurationRoot.GetValue("SkipDLC", options.SkipDLC);
			options.RandomizeRecheckIntervalMs = configurationRoot.GetValue("RandomizeRecheckIntervalMs", options.RandomizeRecheckIntervalMs);
		}
		finally {
			Semaphore.Release();
		}
	}

	private static IConfigurationRoot CreateConfigurationRoot() {
		IConfigurationRoot configurationRoot = new ConfigurationBuilder()
			.SetBasePath(Path.GetFullPath(BasePath))
			.AddJsonFile(DefaultJsonFile, true, false)
			.Build();

		return configurationRoot;
	}

	private static readonly SemaphoreSlim Semaphore = new(1, 1);

	public static async Task Save(ASFFreeGamesOptions options, CancellationToken cancellationToken) {
		string path = Path.Combine(BasePath, DefaultJsonFile);

		await Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try {
#pragma warning disable CAC001
#pragma warning disable CA2007
			await using FileStream fs = new(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
#pragma warning restore CA2007
#pragma warning restore CAC001
			await JsonSerializer.SerializeAsync(fs, options, new JsonSerializerOptions { WriteIndented = true }, cancellationToken).ConfigureAwait(false);
		}
		finally {
			Semaphore.Release();
		}
	}

	public static string BasePath => SharedInfo.ConfigDirectory;
	public const string DefaultJsonFile = "freegames.json.config";
}
