using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Maxisoft.Utils.Collections.Spans {
	[SuppressMessage("Usage", "CA2225")]
	public ref partial struct BitSpan {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ReadOnlySpan<long>(BitSpan bs) {
			return bs.Span;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Span<long>(BitSpan bs) {
			return bs.Span;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator BitSpan(Span<long> span) {
			return new BitSpan(span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator BitSpan(Span<int> span) {
			return new BitSpan(MemoryMarshal.Cast<int, long>(span));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator BitArray(BitSpan span) {
			return span.ToBitArray();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator BitSpan([NotNull] BitArray bitArray) {
			var arr = new int[ComputeLongArraySize(bitArray.Count) * (sizeof(long) / sizeof(int))];
			bitArray.CopyTo(arr, 0);

			return (Span<int>) arr;
		}

		public readonly int CompareTo(BitSpan other) {
			var limit = Math.Min(Span.Length, other.Span.Length);

			for (var i = 0; i < limit; i++) {
				var c = Span[i].CompareTo(other.Span[i]);

				if (c != 0) {
					return c;
				}
			}

			return Span.Length.CompareTo(other.Span.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(BitSpan other) {
			if (Length != other.Length) {
				return false;
			}

			return CompareTo(other) == 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(BitSpan left, BitSpan right) {
			return left.Equals(right);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(BitSpan left, BitSpan right) {
			return !(left == right);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BitSpan operator &(BitSpan left, BitSpan right) {
			var buff = new long[Math.Max(left.Span.Length, right.Span.Length)];
			var res = CreateFromBuffer<long>(buff);
			left.Span.CopyTo(res);

			return res.And(right);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BitSpan operator |(BitSpan left, BitSpan right) {
			var buff = new long[Math.Max(left.Span.Length, right.Span.Length)];
			var res = CreateFromBuffer<long>(buff);
			left.Span.CopyTo(res);

			return res.Or(right);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BitSpan operator ^(BitSpan left, BitSpan right) {
			var buff = new long[Math.Max(left.Span.Length, right.Span.Length)];
			var res = CreateFromBuffer<long>(buff);
			left.Span.CopyTo(res);

			return res.Xor(right);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BitSpan operator ~(BitSpan bs) {
			var buff = new long[bs.Count];
			var res = CreateFromBuffer<long>(buff);
			bs.Span.CopyTo(res);

			return res.Not();
		}

		internal readonly bool IsTrue() {
			if (Span.IsEmpty) {
				return false;
			}

			foreach (var l in Span) {
				if (l != 0) {
					return false;
				}
			}

			return true;
		}

		public readonly bool IsZero => !IsTrue();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator true(BitSpan bs) {
			return bs.IsTrue();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator false(BitSpan bs) {
			return !bs.IsTrue();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(BitSpan left, BitSpan right) {
			return left.CompareTo(right) < 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(BitSpan left, BitSpan right) {
			return left.CompareTo(right) > 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(BitSpan left, BitSpan right) {
			return left.CompareTo(right) <= 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(BitSpan left, BitSpan right) {
			return left.CompareTo(right) >= 0;
		}
	}
}
