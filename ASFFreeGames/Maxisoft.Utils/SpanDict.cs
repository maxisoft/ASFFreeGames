using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Maxisoft.Utils.Collections.Spans {
	/// <summary>
	/// </summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
	/// <remarks>
	///     Use <see cref="!:https://en.wikipedia.org/wiki/Open_addressing">Linear probing</see> as collisions resolution
	/// </remarks>
	[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
	[DebuggerTypeProxy(typeof(SpanDict<,>.DebuggerTypeProxyImpl))]
	[SuppressMessage("Design", "CA1051")]
	public ref partial struct SpanDict<TKey, TValue> where TKey : notnull {
		public readonly Span<KeyValuePair<TKey, TValue>> Buckets;
		internal BitSpan Mask;
		public readonly IEqualityComparer<TKey> Comparer;

		public SpanDict(int capacity, IEqualityComparer<TKey>? comparer = null) : this(new KeyValuePair<TKey, TValue>[capacity], comparer) { }

		public SpanDict(
			Span<KeyValuePair<TKey, TValue>> buckets, BitSpan mask,
			IEqualityComparer<TKey>? comparer = null
		) {
			if (mask.Count < buckets.Length) {
				throw new ArgumentException("mask isn't large enough", nameof(mask));
			}

			Buckets = buckets;
			Mask = mask;
			Comparer = comparer ?? EqualityComparer<TKey>.Default;
			Count = 0;
		}

		/// <summary>
		/// </summary>
		/// <param name="buckets"></param>
		/// <param name="comparer"></param>
		/// <remarks>This constructor <b>Allocate</b> an array in order to store the <see cref="Mask" /></remarks>
		public SpanDict(Span<KeyValuePair<TKey, TValue>> buckets, IEqualityComparer<TKey>? comparer = null) : this(buckets, BitSpan.Zeros(buckets.Length), comparer) { }

		public readonly Enumerator GetEnumerator() {
			return new Enumerator(this);
		}

		public void Add(in KeyValuePair<TKey, TValue> item) {
			Add(item.Key, item.Value);
		}

		public void Clear() {
			Count = 0;
			Mask.SetAll(false);
			Buckets.Clear();
		}

		public readonly bool Contains(in KeyValuePair<TKey, TValue> item) {
			return ContainsKey(item.Key);
		}

		public readonly void CopyTo([NotNull] KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
			if ((arrayIndex < 0) || (arrayIndex > array.Length)) {
				throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Out of bounds");
			}

			if (array.Length - arrayIndex < Count) {
				throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Out of bounds");
			}

			Span<KeyValuePair<TKey, TValue>> span = array;
			CopyTo(span.Slice(arrayIndex));
		}

		public readonly void CopyTo(Span<KeyValuePair<TKey, TValue>> span) {
			if (span.Length < Count) {
				throw new ArgumentOutOfRangeException(nameof(span), "Out of bounds");
			}

			var c = 0;

			for (var i = 0; i < Capacity; i++) {
				if (!Mask[i]) {
					continue;
				}

				span[c] = Buckets[i];
				c++;

				if (c >= Count) {
					break;
				}
			}
		}

		public bool Remove(in KeyValuePair<TKey, TValue> item) {
			return Remove(item.Key);
		}

		public readonly int Capacity => Buckets.Length;

		public int Count { get; internal set; }

		public readonly bool IsReadOnly => Count == Capacity;

		public void Add(in TKey key, in TValue value) {
			if (Count >= Capacity) {
				throw new InvalidOperationException($"{nameof(Buckets)} full");
			}

			var index = SearchFor(in key);

			if (index >= 0) {
				throw new ArgumentException("key already exists", nameof(key));
			}

			index = ~index;

			if (Mask[index]) {
				throw new InvalidOperationException($"{nameof(Buckets)} full");
			}

			Buckets[index] = new KeyValuePair<TKey, TValue>(key, value);
			Mask.Set(index, true);
			Count += 1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private readonly int ComputeIndex(in TKey key) {
			return (Comparer.GetHashCode(key) & int.MaxValue) % Capacity;
		}

		internal readonly int SearchFor(in TKey key) {
			var h = ComputeIndex(in key);

			var forward = h;
			var c = 0;
			var limit = Capacity;

			do {
				if (!Mask[forward]) {
					break;
				}

				if (Comparer.Equals(Buckets[forward].Key, key)) {
					return forward;
				}

				forward = (forward + 1) % limit;
			} while (c++ < limit);

			return ~forward;
		}

		public readonly int IndexOf(in TKey key) {
			var res = SearchFor(in key);

			return Math.Max(-1, res);
		}

		public readonly bool ContainsKey(in TKey key) {
			return SearchFor(in key) >= 0;
		}

		internal readonly int Distance(int from, int to) {
			return ((to - @from) + Capacity) % Capacity;
		}

		public bool Remove(in TKey key) {
			var index = IndexOf(in key);

			if (index < 0) {
				return false;
			}

			return RemoveAt(index);
		}

		private bool RemoveAt(int index) {
			var originalIndex = index;
			Mask.Set(index, false);
			Count -= 1;

			if (Capacity <= 1) {
				return true;
			}

			var forward = (index + 1) % Capacity;
			var c = 0;
			var limit = Capacity;

			do {
				if (!Mask[forward]) {
					break;
				}

				var h = ComputeIndex(Buckets[forward].Key);

				if (Distance(h, index) <= Distance(h, forward)) {
					Buckets[index] = Buckets[forward];
					Mask.Set(index, true);
					Mask.Set(forward, false);

					if (RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey, TValue>>()) {
						Buckets[forward] = default;
					}

					index = forward;
				}

				forward = (forward + 1) % Capacity;
			} while (c++ < limit);

			if (RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey, TValue>>() && !Mask[originalIndex]) {
				Buckets[originalIndex] = default;
			}

			return true;
		}

		public readonly bool TryGetValue(in TKey key, out TValue value) {
			var index = IndexOf(in key);

			if (index < 0) {
				value = default!;

				return false;
			}

			value = Buckets[index].Value;

			return true;
		}

		public TValue this[in TKey key] {
			get {
				var index = IndexOf(in key);

				if (index < 0) {
					throw new KeyNotFoundException();
				}

				return Buckets[index].Value;
			}
			set {
				var index = IndexOf(in key);

				if (index >= 0) {
					Buckets[index] = new KeyValuePair<TKey, TValue>(key, value);
				}
				else {
					Add(in key, in value);
				}
			}
		}

		public KeyEnumerator Keys => new KeyEnumerator(this);

		public ValueEnumerator Values => new ValueEnumerator(this);

		[SuppressMessage("Design", "CA1000")]
		public static SpanDict<TKey, TValue> CreateFromBuffers<TBucket, TMask>(
			Span<TBucket> buckets, Span<TMask> mask,
			IEqualityComparer<TKey>? comparer = null, int count = -1
		) where TBucket : struct where TMask : struct {
			var cBucket = MemoryMarshal.Cast<TBucket, KeyValuePair<TKey, TValue>>(buckets);
			var cMask = MemoryMarshal.Cast<TMask, long>(mask);
			var res = new SpanDict<TKey, TValue>(cBucket, cMask, comparer);

			if (count >= 0) {
				res.Count = count;

				if ((uint) count > (uint) res.Capacity) {
					throw new ArgumentOutOfRangeException(nameof(count), count, null);
				}

				return res;
			}

			count = 0;

			foreach (var exists in res.Mask) {
				count += exists ? 1 : 0;
			}

			res.Count = count;

			if ((uint) count > (uint) res.Capacity) {
				throw new ArgumentOutOfRangeException(nameof(mask));
			}

			return res;
		}

		[SuppressMessage("Design", "CA1000")]
		public static SpanDict<TKey, TValue> CreateFromBuffer<TSpan>(
			Span<TSpan> buff,
			IEqualityComparer<TKey>? comparer = null, int count = -1
		) where TSpan : unmanaged {
			unsafe {
				while ((buff.Length > 0) && ((buff.Length * sizeof(TSpan)) % sizeof(long) != 0)) {
					buff = buff.Slice(0, buff.Length - 1);
				}
			}

			var kvSpan = MemoryMarshal.Cast<TSpan, KeyValuePair<TKey, TValue>>(buff);
			var reserved = BitSpan.ComputeLongArraySize(kvSpan.Length);
			var longSpan = MemoryMarshal.Cast<TSpan, long>(buff);

			return CreateFromBuffers(longSpan.Slice(reserved), longSpan.Slice(0, reserved), comparer, count);
		}
	}
}
