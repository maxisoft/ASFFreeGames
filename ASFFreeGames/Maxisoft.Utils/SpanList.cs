using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Maxisoft.Utils.Collections.Spans {
	[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
	[DebuggerTypeProxy(typeof(SpanList<>.DebuggerTypeProxyImpl))]
	public ref struct SpanList<T> {
		internal readonly Span<T> Span;

		public SpanList(Span<T> span, int count = 0) {
			if ((uint) count > (uint) span.Length) {
				throw new ArgumentOutOfRangeException(nameof(count), count, null);
			}

			Span = span;
			Count = count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly Span<T> AsSpan() {
			return Span.Slice(0, Count);
		}

		public readonly int Capacity => Span.Length;

		public static implicit operator Span<T>(SpanList<T> list) {
			return list.AsSpan();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly Span<T>.Enumerator GetEnumerator() {
			return AsSpan().GetEnumerator();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<T> Data() {
			return Span;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(in T item) {
			if ((uint) Count >= (uint) Span.Length) {
				throw new InvalidOperationException("span is full");
			}

			Span[Count++] = item;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Clear() {
			Count = 0;
			Span.Clear();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Contains(in T item) {
			return IndexOf(in item) >= 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly void CopyTo(T[] array, int arrayIndex) {
			Span<T> destination = array;
			AsSpan().CopyTo(destination.Slice(arrayIndex));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Remove(in T item) {
			var index = IndexOf(in item);

			if (index < 0) {
				return false;
			}

			RemoveAt(index);

			return true;
		}

		public int Count { get; internal set; }

		public readonly bool IsReadOnly => Count == Capacity;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int IndexOf(in T item) {
			var comparer = EqualityComparer<T>.Default;

			return IndexOf(in item, in comparer);
		}

		public readonly int IndexOf<TEqualityComparer>(in T item, in TEqualityComparer comparer)
			where TEqualityComparer : IEqualityComparer<T> {
			var c = 0;

			foreach (var element in AsSpan()) {
				if (comparer.Equals(element, item)) {
					return c;
				}

				c += 1;
			}

			return -1;
		}

		public void Insert(int index, in T item) {
			if ((uint) Count >= (uint) Span.Length) {
				throw new InvalidOperationException("span is full");
			}

			if (index == Count) {
				Add(in item);

				return;
			}

			AsSpan().Slice(index, Count - index).CopyTo(Span.Slice(index + 1));
			Span[index] = item;
			Count += 1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void RemoveAt(int index) {
			AsSpan().Slice(index + 1, Count - 1 - index).CopyTo(Span.Slice(index));
			Count -= 1;
		}

		public readonly ref T this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref AsSpan()[index];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly ref T At(WrappedIndex index) {
			return ref AsSpan()[index.Resolve(Count)];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly ref T Front() {
			CheckForOutOfBounds(0, nameof(Count));

			return ref Span[0];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly ref T Back() {
			CheckForOutOfBounds(Count - 1, nameof(Count));

			return ref Span[Count - 1];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private readonly void CheckForOutOfBounds(
			int index, string paramName,
			string message =
				"Index was out of range. Must be non-negative and less than the size of the collection."
		) {
			if ((uint) index >= (uint) Count) {
				throw new ArgumentOutOfRangeException(paramName, index, message);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly Span<T> GetSlice(int index, int count) {
			return AsSpan().Slice(index, count);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly T[] ToArray() {
			return AsSpan().ToArray();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SuppressMessage("Design", "CA1002")]
		public readonly List<T> ToList() {
			var res = new List<T>(Count);

			foreach (var item in AsSpan()) {
				res.Add(item);
			}

			return res;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly TList ToList<TList>() where TList : IList<T>, new() {
			var res = new TList();

			foreach (var item in AsSpan()) {
				res.Add(item);
			}

			return res;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reverse() {
			Reverse(0, Count);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reverse(int index, int count) {
			GetSlice(index, count).Reverse();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int BinarySearch(int index, int count, in T item, IComparer<T>? comparer = null) {
			comparer ??= Comparer<T>.Default;

			return GetSlice(index, count).BinarySearch(item, comparer);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int BinarySearch(in T item, IComparer<T>? comparer = null) {
			return BinarySearch(0, Count, in item, comparer);
		}

		[DebuggerNonUserCode]
		private readonly ref struct DebuggerTypeProxyImpl {
			private readonly SpanList<T> _list;

			public DebuggerTypeProxyImpl(SpanList<T> list) {
				_list = list;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
			public long Count => _list.Count;

			[DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
			public long Capacity => _list.Capacity;

			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public Span<T> Span => _list.Span;

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public T[] Items => _list.ToArray();
		}

		public readonly Span<T> ToSpan() => Span;
	}
}
