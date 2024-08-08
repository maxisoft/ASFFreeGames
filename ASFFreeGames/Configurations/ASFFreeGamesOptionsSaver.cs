using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace ASFFreeGames.Configurations;

public static class ASFFreeGamesOptionsSaver {
	public static async Task<int> SaveOptions([NotNull] Stream stream, [NotNull] ASFFreeGamesOptions options, bool checkValid = true, CancellationToken cancellationToken = default) {
		using IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(1 << 15);
		int written = CreateOptionsBuffer(options, memory);

		if (checkValid) {
			PseudoValidate(memory, written);
		}

		await stream.WriteAsync(memory.Memory[..written], cancellationToken).ConfigureAwait(false);

		return written;
	}

	private static void PseudoValidate(IMemoryOwner<byte> memory, int written) {
		JsonNode? doc = JsonNode.Parse(Encoding.UTF8.GetString(memory.Memory[..written].Span));

		doc?["skipFreeToPlay"]?.GetValue<bool?>();
	}

	internal static int CreateOptionsBuffer(ASFFreeGamesOptions options, IMemoryOwner<byte> memory) {
		Span<byte> buffer = memory.Memory.Span;
		buffer.Clear();

		int written = 0;
		written += WriteJsonString("{\n"u8, buffer, written);

		written += WriteNameAndProperty("recheckInterval"u8, options.RecheckInterval, buffer, written);
		written += WriteNameAndProperty("randomizeRecheckInterval"u8, options.RandomizeRecheckInterval, buffer, written);
		written += WriteNameAndProperty("skipFreeToPlay"u8, options.SkipFreeToPlay, buffer, written);
		written += WriteNameAndProperty("skipDLC"u8, options.SkipDLC, buffer, written);
		written += WriteNameAndProperty("blacklist"u8, options.Blacklist, buffer, written);
		written += WriteNameAndProperty("verboseLog"u8, options.VerboseLog, buffer, written);
		written += WriteNameAndProperty("proxy"u8, options.Proxy, buffer, written);
		written += WriteNameAndProperty("redditProxy"u8, options.RedditProxy, buffer, written);
		RemoveTrailingCommaAndLineReturn(buffer, ref written);

		written += WriteJsonString("\n}"u8, buffer, written);

		// Resize buffer if needed
		if (written >= buffer.Length) {
			throw new InvalidOperationException("Buffer overflow while saving options");
		}

		return written;
	}

	private static void RemoveTrailingCommaAndLineReturn(Span<byte> buffer, ref int written) {
		int c;

		do {
			c = RemoveTrailing(buffer, "\n"u8, ref written);
			c += RemoveTrailing(buffer, ","u8, ref written);
		} while (c > 0);
	}

	private static int RemoveTrailing(Span<byte> buffer, ReadOnlySpan<byte> target, ref int written) {
		Span<byte> sub = buffer[..written];
		int c = 0;

		while (!sub.IsEmpty) {
			if (sub.EndsWith(target)) {
				written -= target.Length;
				sub = sub[..written];
				c += 1;
			}
			else {
				break;
			}
		}

		return c;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static int WriteEscapedJsonString(string str, Span<byte> buffer, int written) {
		const byte quote = (byte) '"';
		const byte backslash = (byte) '\\';

		int startIndex = written;
		buffer[written++] = quote;
		Span<char> cstr = stackalloc char[1];
		ReadOnlySpan<char> span = str.AsSpan();

		// ReSharper disable once ForCanBeConvertedToForeach
		for (int index = 0; index < span.Length; index++) {
			char c = span[index];

			switch (c) {
				case '"':
					buffer[written++] = backslash;
					buffer[written++] = quote;

					break;
				case '\\':
					buffer[written++] = backslash;
					buffer[written++] = backslash;

					break;
				case '\b':
					buffer[written++] = backslash;
					buffer[written++] = (byte) 'b';

					break;
				case '\f':
					buffer[written++] = backslash;
					buffer[written++] = (byte) 'f';

					break;
				case '\n':
					buffer[written++] = backslash;
					buffer[written++] = (byte) 'n';

					break;
				case '\r':
					buffer[written++] = backslash;
					buffer[written++] = (byte) 'r';

					break;
				case '\t':
					buffer[written++] = backslash;
					buffer[written++] = (byte) 't';

					break;
				default:
					// Optimize for common case of ASCII characters
					if (c < 128) {
						buffer[written++] = (byte) c;
					}
					else {
						cstr[0] = c;
						written += WriteJsonString(cstr, buffer, written);
					}

					break;
			}
		}

		buffer[written++] = quote;

		return written - startIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static int WriteNameAndProperty<T>(ReadOnlySpan<byte> name, T value, Span<byte> buffer, int written) {
		int startIndex = written;
		written += WriteJsonString("\""u8, buffer, written);
		written += WriteJsonString(name, buffer, written);
		written += WriteJsonString("\": "u8, buffer, written);

		if (value is null) {
			written += WriteJsonString("null"u8, buffer, written);
		}
		else {
			written += value switch {
				string str => WriteEscapedJsonString(str, buffer, written),
#pragma warning disable CA1308
				bool b => WriteJsonString(b ? "true"u8 : "false"u8, buffer, written),
#pragma warning restore CA1308
				IReadOnlyCollection<string> collection => WriteJsonArray(collection, buffer, written),
				TimeSpan timeSpan => WriteEscapedJsonString(timeSpan.ToString(), buffer, written),
				_ => throw new ArgumentException($"Unsupported type for property {Encoding.UTF8.GetString(name)}: {value.GetType()}")
			};
		}

		written += WriteJsonString(","u8, buffer, written);
		written += WriteJsonString("\n"u8, buffer, written);

		return written - startIndex;
	}

	private static int WriteJsonArray(IEnumerable<string> collection, Span<byte> buffer, int written) {
		int startIndex = written;
		written += WriteJsonString("["u8, buffer, written);
		bool first = true;

		foreach (string item in collection) {
			if (!first) {
				written += WriteJsonString(","u8, buffer, written);
			}

			written += WriteEscapedJsonString(item, buffer, written);
			first = false;
		}

		written += WriteJsonString("]"u8, buffer, written);

		return written - startIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
	private static int WriteJsonString(ReadOnlySpan<byte> str, Span<byte> buffer, int written) {
		str.CopyTo(buffer[written..(written + str.Length)]);

		return str.Length;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
	private static int WriteJsonString(ReadOnlySpan<char> str, Span<byte> buffer, int written) {
		int encodedLength = Encoding.UTF8.GetBytes(str, buffer[written..]);

		return encodedLength;
	}
}
