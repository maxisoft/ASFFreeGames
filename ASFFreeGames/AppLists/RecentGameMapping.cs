using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ASFFreeGames.ASFExtentions.Games;
using Maxisoft.ASF.ASFExtentions;
using Maxisoft.Utils.Collections.Spans;

namespace Maxisoft.ASF.AppLists;

public class RecentGameMapping {
	private static ReadOnlySpan<byte> MagicBytes => "mdict"u8;
	private readonly Memory<long> Buffer;
	private Memory<long> SizeMemory;
	private Memory<long> DictData;

	public RecentGameMapping(Memory<long> buffer, bool reset = true) {
		Buffer = buffer;

		if (reset) {
			InitMemories();
		}
		else {
			LoadMemories(false);
		}
	}

	internal void InitMemories() {
		if (MagicBytes.Length > sizeof(long)) {
#pragma warning disable CA2201
			throw new Exception();
#pragma warning restore CA2201
		}

		MagicBytes.CopyTo(MemoryMarshal.Cast<long, byte>(Buffer.Span)[..MagicBytes.Length]);

		int start = 1;

		SizeMemory = Buffer.Slice(start, ++start);
		DictData = Buffer[start..];

		DictData.Span.Clear();
		CountRef = 0;
	}

	public void Reload(bool fix = false) => LoadMemories(fix);
	public void Reset() => InitMemories();

	internal void LoadMemories(bool allowFixes) {
		ReadOnlySpan<byte> magicBytes = MagicBytes;
		ReadOnlySpan<byte> magicSpan = MemoryMarshal.Cast<long, byte>(Buffer.Span)[..magicBytes.Length];

		// ReSharper disable once LoopCanBeConvertedToQuery
		for (int i = 0; i < magicBytes.Length; i++) {
			if (magicSpan[i] != magicBytes[i]) {
				if (allowFixes) {
					Reset();

					continue;
				}

				throw new InvalidDataException();
			}
		}

		int start = 1;

		SizeMemory = Buffer.Slice(start, ++start);
		DictData = Buffer[start..];

		if ((CountRef < 0) && !allowFixes) {
			throw new InvalidDataException();
		}

		SpanDict<GameIdentifier, long> dict = SpanDict<GameIdentifier, long>.CreateFromBuffer(DictData.Span);

		if (dict.Count != CountRef) {
			if (!allowFixes) {
				throw new InvalidDataException("Counters mismatch");
			}

			CountRef = dict.Count;
		}
	}

	public long Count => CountRef;

	internal ref long CountRef => ref SizeMemory.Span[0];

	public SpanDict<GameIdentifier, long> Dict => SpanDict<GameIdentifier, long>.CreateFromBuffer(DictData.Span, null, checked((int) Count));

	public bool Contains(in GameIdentifier item) => TryGetDate(in item, out long date) && (date > 0);

	public bool ContainsInvalid(in GameIdentifier item) => TryGetDate(in item, out long date) && (date < 0);

	public bool TryGetDate(in GameIdentifier key, out long value) => Dict.TryGetValue(in key, out value);

	public bool Add(in GameIdentifier item) => Add(in item, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

	public bool AddInvalid(in GameIdentifier item) => Add(in item, -DateTimeOffset.UtcNow.ToUnixTimeSeconds());

	public bool Add(in GameIdentifier item, long date) {
		SpanDict<GameIdentifier, long> dict = Dict;

		if (EnsureFillFactor(ref dict) != 0) {
			CountRef = dict.Count;
		}

		if (!dict.ContainsKey(in item)) {
			dict.Add(in item, date);
			CountRef = dict.Count;

			return true;
		}

		return false;
	}

	private static int EnsureFillFactor(ref SpanDict<GameIdentifier, long> dict, int maxIter = 32) {
		int c = maxIter;
		int res = 0;

		if (dict.Capacity * 8 < dict.Count * 10) {
			while ((dict.Count > 0) && (c-- > 0)) {
				long minValue = long.MaxValue;
				GameIdentifier minId = default;

				foreach (ref KeyValuePair<GameIdentifier, long> pair in dict) {
					long value = Math.Abs(pair.Value);

					if (value <= minValue) {
						minValue = value;
						minId = pair.Key;
					}
				}

				if (dict.Remove(in minId)) {
					res++;
				}
			}
		}

		return res;
	}
}
