using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm;
using ASFFreeGames.Commands.GetIp;
using ASFFreeGames.Configurations;
using Microsoft.Extensions.Configuration;

namespace Maxisoft.ASF.Configurations;

public static class ASFFreeGamesOptionsLoader {
	public static void Bind(ref ASFFreeGamesOptions options) {
		// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
		options ??= new ASFFreeGamesOptions();
		Semaphore.Wait();

		try {
			IConfigurationRoot configurationRoot = CreateConfigurationRoot();

			IEnumerable<string> blacklist = configurationRoot.GetValue("Blacklist", options.Blacklist) ?? options.Blacklist;
			options.Blacklist = new HashSet<string>(blacklist, StringComparer.InvariantCultureIgnoreCase);

			options.VerboseLog = configurationRoot.GetValue("VerboseLog", options.VerboseLog);
			options.RecheckInterval = TimeSpan.FromMilliseconds(configurationRoot.GetValue("RecheckIntervalMs", options.RecheckInterval.TotalMilliseconds));
			options.SkipFreeToPlay = configurationRoot.GetValue("SkipFreeToPlay", options.SkipFreeToPlay);
			options.SkipDLC = configurationRoot.GetValue("SkipDLC", options.SkipDLC);
			options.RandomizeRecheckInterval = configurationRoot.GetValue("RandomizeRecheckInterval", options.RandomizeRecheckInterval);
			options.Proxy = configurationRoot.GetValue("Proxy", options.Proxy);
			options.RedditProxy = configurationRoot.GetValue("RedditProxy", options.RedditProxy);
		}
		finally {
			Semaphore.Release();
		}
	}

	private static IConfigurationRoot CreateConfigurationRoot() {
		IConfigurationRoot configurationRoot = new ConfigurationBuilder()
			.SetBasePath(Path.GetFullPath(BasePath))
			.AddJsonFile(DefaultJsonFile, true, false)
			.AddEnvironmentVariables("FREEGAMES_")
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

			// Use FileOptions.Asynchronous when creating a file stream for async operations
			await using FileStream fs = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
#pragma warning restore CA2007
#pragma warning restore CAC001
			using IMemoryOwner<byte> buffer = MemoryPool<byte>.Shared.Rent(checked(fs.Length > 0 ? (int) fs.Length + 1 : 1 << 15));
			int read = await fs.ReadAsync(buffer.Memory, cancellationToken).ConfigureAwait(false);

			try {
				fs.Position = 0;
				fs.SetLength(0);
				int written = await ASFFreeGamesOptionsSaver.SaveOptions(fs, options, true, cancellationToken).ConfigureAwait(false);
				fs.SetLength(written);
			}

			catch (Exception) {
				fs.Position = 0;
				await fs.WriteAsync(buffer.Memory[..read], cancellationToken).ConfigureAwait(false);
				fs.SetLength(read);

				throw;
			}
		}
		finally {
			Semaphore.Release();
		}
	}

	public static string BasePath => SharedInfo.ConfigDirectory;
	public const string DefaultJsonFile = "freegames.json.config";
}
