using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ASFFreeGames.ASFExtentions.Games;
using Maxisoft.ASF.ASFExtentions;

namespace Maxisoft.ASF;

internal sealed class CompletedAppList : IDisposable {
	private long[]? CompletedAppBuffer;
	private const int CompletedAppBufferSize = 128;
	private Memory<long> CompletedAppMemory => ((Memory<long>) CompletedAppBuffer!)[..CompletedAppBufferSize];
	private readonly RecentGameMapping CompletedApps;
	private const int FileCompletedAppBufferSize = CompletedAppBufferSize * sizeof(long) * 2;
	private static readonly ArrayPool<long> LongMemoryPool = ArrayPool<long>.Create(CompletedAppBufferSize, 10);
	private static readonly char Endianness = BitConverter.IsLittleEndian ? 'l' : 'b';
	public static readonly string FileExtension = $".fg{Endianness}dict";

	public CompletedAppList() {
		CompletedAppBuffer = LongMemoryPool.Rent(CompletedAppBufferSize);
		CompletedApps = new RecentGameMapping(CompletedAppMemory);
	}

	~CompletedAppList() => ReturnBuffer();

	private bool ReturnBuffer() {
		if (CompletedAppBuffer is { Length: > 0 }) {
			LongMemoryPool.Return(CompletedAppBuffer);

			return true;
		}

		return false;
	}

	public void Dispose() {
		if (ReturnBuffer()) {
			CompletedAppBuffer = Array.Empty<long>();
		}

		GC.SuppressFinalize(this);
	}

	[SuppressMessage("Code", "CAC001:ConfigureAwaitChecker")]
	public async Task SaveToFile(string filePath, CancellationToken cancellationToken = default) {
		if (string.IsNullOrWhiteSpace(filePath)) {
			return;
		}
#pragma warning disable CA2007
		await using FileStream sourceStream = new(
			filePath,
			FileMode.Create, FileAccess.Write, FileShare.None,
			bufferSize: FileCompletedAppBufferSize, useAsync: true
		);

		// ReSharper disable once UseAwaitUsing
		using BrotliStream encoder = new(sourceStream, CompressionMode.Compress);

		ChangeBrotliEncoderToFastCompress(encoder);
#pragma warning restore CA2007

		// note: cannot use WriteAsync call due to span & async incompatibilities
		// but it shouldn't be an issue as we use a bigger bufferSize than the written payload
		encoder.Write(MemoryMarshal.Cast<long, byte>(CompletedAppMemory.Span));
		await encoder.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	[SuppressMessage("Code", "CAC001:ConfigureAwaitChecker")]
	public async Task LoadFromFile(string filePath, CancellationToken cancellationToken = default) {
		if (string.IsNullOrWhiteSpace(filePath)) {
			return;
		}

		try {
#pragma warning disable CA2007
			await using FileStream sourceStream = new(
				filePath,
				FileMode.Open, FileAccess.Read, FileShare.Read,
				bufferSize: FileCompletedAppBufferSize, useAsync: true
			);

			// ReSharper disable once UseAwaitUsing
			using BrotliStream decoder = new(sourceStream, CompressionMode.Decompress);
#pragma warning restore CA2007
			ChangeBrotliEncoderToFastCompress(decoder);

			// ReSharper disable once UseAwaitUsing
			using MemoryStream ms = new();
			await decoder.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
			await decoder.FlushAsync(cancellationToken).ConfigureAwait(false);

			if (CompletedAppBuffer is { Length: > 0 } && (ms.Length == CompletedAppMemory.Length * sizeof(long))) {
				ms.Seek(0, SeekOrigin.Begin);
				int size = ms.Read(MemoryMarshal.Cast<long, byte>(CompletedAppMemory.Span));

				if (size != CompletedAppMemory.Length * sizeof(long)) {
					ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericError("[FreeGames] Unable to load previous completed app dict", nameof(LoadFromFile));
				}

				try {
					CompletedApps.Reload();
				}
				catch (InvalidDataException e) {
					ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericWarningException(e, $"[FreeGames] {nameof(CompletedApps)}.{nameof(CompletedApps.Reload)}");
					CompletedApps.Reload(true);
				}
			}
			else {
				ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericError("[FreeGames] Unable to load previous completed app dict", nameof(LoadFromFile));
			}
		}
		catch (FileNotFoundException) {
			return;
		}
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

	public bool Add(in GameIdentifier gameIdentifier) => CompletedApps.Add(in gameIdentifier);
	public bool AddInvalid(in GameIdentifier gameIdentifier) => CompletedApps.AddInvalid(in gameIdentifier);

	public bool Contains(in GameIdentifier gameIdentifier) => CompletedApps.Contains(in gameIdentifier);

	public bool ContainsInvalid(in GameIdentifier gameIdentifier) => CompletedApps.ContainsInvalid(in gameIdentifier);
}
