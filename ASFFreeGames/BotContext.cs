using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using BloomFilter;

namespace Maxisoft.ASF;

internal sealed class BotContext : IDisposable {
	private const ulong BlackListCount = 5;
	private long[]? CompletedAppBuffer;
	private Memory<long> CompletedAppMemory => ((Memory<long>) CompletedAppBuffer!)[..CompletedAppBufferSize];
	private readonly RecentGameMapping CompletedApp;
	private readonly Dictionary<GameIdentifier, (ulong counter, DateTime date)> InMemoryCompletedApp = new();
	private readonly WeakReference<Bot> Bot;
	private readonly TimeSpan InMemoryTimeOut = TimeSpan.FromDays(1);
	private const int CompletedAppBufferSize = 128;
	private const int FileCompletedAppBufferSize = CompletedAppBufferSize * sizeof(long) * 2;
	private static readonly ArrayPool<long> LongMemoryPool = ArrayPool<long>.Create(CompletedAppBufferSize, 10);

	private static readonly char Endianness = BitConverter.IsLittleEndian ? 'l' : 'b';
	public static readonly string FileExtension = $".fg{Endianness}dict";

	public BotContext(Bot bot) {
		Bot = new WeakReference<Bot>(bot);
		CompletedAppBuffer = LongMemoryPool.Rent(CompletedAppBufferSize);
		CompletedApp = new RecentGameMapping(CompletedAppMemory);
	}

	private string CompletedAppFilePath() {
		if (!Bot.TryGetTarget(out var bot)) {
			return string.Empty;
		}

		string file = bot.GetFilePath(ArchiSteamFarm.Steam.Bot.EFileType.Config);

		string res = file.Replace(".json", FileExtension, StringComparison.InvariantCultureIgnoreCase);

		if (res == file) {
			throw new FormatException("unable to replace json ext");
		}

		return res;
	}

	public void SaveApp(in GameIdentifier gameIdentifier) {
		if (!CompletedApp.Add(in gameIdentifier)) {
			InMemoryCompletedApp[gameIdentifier] = (long.MaxValue, DateTime.MaxValue - InMemoryTimeOut);
		}
	}

	public bool HasApp(in GameIdentifier gameIdentifier) {
		if (!gameIdentifier.Valid) {
			return false;
		}

		if (InMemoryCompletedApp.TryGetValue(gameIdentifier, out var tuple) && (tuple.counter >= BlackListCount)) {
			if (DateTime.UtcNow - tuple.date > InMemoryTimeOut) {
				InMemoryCompletedApp.Remove(gameIdentifier);
			}
			else {
				return true;
			}
		}

		if (CompletedApp.Contains(in gameIdentifier)) {
			return true;
		}

		if (!Bot.TryGetTarget(out var bot)) {
			return false;
		}

		return bot.OwnedPackageIDs.ContainsKey(checked((uint) gameIdentifier.Id));
	}

	public ulong AppTickCount(in GameIdentifier gameIdentifier, bool increment = false) {
		ulong res = 0;
		DateTime? dateTime = null;

		if (InMemoryCompletedApp.TryGetValue(gameIdentifier, out var tuple)) {
			if (DateTime.UtcNow - tuple.date > InMemoryTimeOut) {
				InMemoryCompletedApp.Remove(gameIdentifier);
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

			InMemoryCompletedApp[gameIdentifier] = (res, dateTime ?? DateTime.UtcNow);
		}

		return res;
	}

	public async Task Save() {
		var filePath = CompletedAppFilePath();

		if (string.IsNullOrWhiteSpace(filePath)) {
			return;
		}
#pragma warning disable CA2007
		await using var sourceStream = new FileStream(
			filePath,
			FileMode.Create, FileAccess.Write, FileShare.None,
			bufferSize: FileCompletedAppBufferSize, useAsync: true
		);

		// ReSharper disable once UseAwaitUsing
		using var encoder = new BrotliStream(sourceStream, CompressionMode.Compress);

		ChangeBrotliEncoderToFastCompress(encoder);
#pragma warning restore CA2007

		// note: cannot use WriteAsync call due to span & async incompatibilities
		// but it shouldn't be an issue as we use a bigger bufferSize than the written payload
		encoder.Write(MemoryMarshal.Cast<long, byte>(CompletedAppMemory.Span));
		await encoder.FlushAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Workaround in order to set brotli's compression level to fastest.
	/// Uses reflexions as the public methods got removed in the ASF public binary.
	/// </summary>
	/// <param name="encoder"></param>
	/// <param name="level"></param>
	private static void ChangeBrotliEncoderToFastCompress(BrotliStream encoder, int level = 1) {
		try {
			FieldInfo? field = encoder.GetType().GetField("_encoder", BindingFlags.NonPublic | BindingFlags.Instance);

			if (field?.GetValue(encoder) is BrotliEncoder previous) {
				BrotliEncoder brotliEncoder = default(BrotliEncoder);

				try {
					MethodInfo? method = brotliEncoder.GetType().GetMethod("SetQuality", BindingFlags.NonPublic | BindingFlags.Instance);
					method?.Invoke(brotliEncoder, new object?[] { level });
					field.SetValue(encoder, brotliEncoder);
				}
				catch (Exception) {
					brotliEncoder.Dispose();

					throw;
				}

				previous.Dispose();
			}
		}
		catch (Exception e) {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericDebuggingException(e, nameof(ChangeBrotliEncoderToFastCompress));
		}
	}

	public async Task Load() {
		var filePath = CompletedAppFilePath();

		if (string.IsNullOrWhiteSpace(filePath)) {
			return;
		}

		try {
#pragma warning disable CA2007
			await using var sourceStream = new FileStream(
				filePath,
				FileMode.Open, FileAccess.Read, FileShare.Read,
				bufferSize: FileCompletedAppBufferSize, useAsync: true
			);

			// ReSharper disable once UseAwaitUsing
			using var decoder = new BrotliStream(sourceStream, CompressionMode.Decompress);
#pragma warning restore CA2007
			ChangeBrotliEncoderToFastCompress(decoder);

			// ReSharper disable once UseAwaitUsing
			using var ms = new MemoryStream();
			await decoder.CopyToAsync(ms).ConfigureAwait(false);
			await decoder.FlushAsync().ConfigureAwait(false);

			if (CompletedAppBuffer is { Length: > 0 } && (ms.Length == CompletedAppMemory.Length * sizeof(long))) {
				ms.Seek(0, SeekOrigin.Begin);
				ms.Read(MemoryMarshal.Cast<long, byte>(CompletedAppMemory.Span));

				try {
					CompletedApp.Reload();
				}
				catch (InvalidDataException e) {
					ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericWarningException(e, $"{nameof(CompletedApp)}.{nameof(CompletedApp.Reload)}");
					CompletedApp.Reload(true);
				}
			}
		}
		catch (FileNotFoundException) {
			return;
		}
	}

	public void Dispose() {
		// ReSharper disable once ConditionIsAlwaysTrueOrFalse
		if (CompletedAppBuffer is { Length: > 0 }) {
			LongMemoryPool.Return(CompletedAppBuffer);
		}

		CompletedAppBuffer = Array.Empty<long>();
	}
}
