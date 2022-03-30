using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Maxisoft.Utils.Collections.Spans {
	[SuppressMessage("Design", "CA1051")]
	public ref partial struct BitSpan {
		public readonly Span<long> Span;
		public const int LongNumBit = sizeof(long) * 8;

		public BitSpan(Span<long> span) {
			Span = span;
		}

		public bool this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Set(index, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private readonly void ThrowForOutOfBounds(int index) {
			if ((uint) index >= LongNumBit * (uint) Span.Length) {
				throw new ArgumentOutOfRangeException(nameof(index), index, null);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Get(int index) {
			ThrowForOutOfBounds(index);

			return (Span[index / LongNumBit] & (1L << (index % LongNumBit))) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, bool value) {
			ThrowForOutOfBounds(index);

			if (value) {
				Span[index / LongNumBit] |= 1L << (index % LongNumBit);
			}
			else {
				Span[index / LongNumBit] &= ~(1L << (index % LongNumBit));
			}
		}

		public void SetAll(bool value) {
			if (value) {
				Span.Fill(unchecked((long) ulong.MaxValue));
			}
			else {
				Span.Clear();
			}
		}

		public BitSpan And(in BitSpan other) {
			if (Span.Length < other.Span.Length) {
				throw new ArgumentException(null, nameof(other));
			}

			for (var i = 0; i < other.Span.Length; i++) {
				Span[i] &= other.Span[i];
			}

			return this;
		}

		public BitSpan Or(in BitSpan other) {
			if (Span.Length < other.Span.Length) {
				throw new ArgumentException(null, nameof(other));
			}

			for (var i = 0; i < other.Span.Length; i++) {
				Span[i] |= other.Span[i];
			}

			return this;
		}

		public BitSpan Xor(in BitSpan other) {
			if (Span.Length < other.Span.Length) {
				throw new ArgumentException(null, nameof(other));
			}

			for (var i = 0; i < other.Span.Length; i++) {
				Span[i] ^= other.Span[i];
			}

			return this;
		}

		public BitSpan Not() {
			for (var i = 0; i < Span.Length; i++) {
				Span[i] = ~Span[i];
			}

			return this;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly Span<T> ASpan<T>() where T : unmanaged {
			return MemoryMarshal.Cast<long, T>(Span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly BitArray ToBitArray() {
			return new BitArray(ASpan<int>().ToArray());
		}

		public readonly int Length => Span.Length * LongNumBit;

		public readonly int Count => Length;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly Enumerator GetEnumerator() {
			return new Enumerator(this);
		}

		public override readonly int GetHashCode() {
			var h = Count.GetHashCode();

			foreach (var l in Span) {
				h = unchecked(31 * h + l.GetHashCode());
			}

			return h;
		}

		public override readonly bool Equals(object? obj) {
			return obj switch {
				BitArray ba => Equals((BitSpan) ba),
				_ => false
			};
		}
	}
}
