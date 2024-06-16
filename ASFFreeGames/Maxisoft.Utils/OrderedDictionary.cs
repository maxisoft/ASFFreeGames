using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

// ReSharper disable once RedundantNullableDirective
#nullable enable

// ReSharper disable once CheckNamespace
namespace Maxisoft.Utils.Collections.Dictionaries {
	/// <summary>
	///     OrderedDictionary abstraction. Implement most <see cref="IOrderedDictionary{TKey,TValue}" />'s operations using
	///     generics.
	/// </summary>
	/// <typeparam name="TKey">The key type.</typeparam>
	/// <typeparam name="TValue">The Value type.</typeparam>
	/// <typeparam name="TList">The <see cref="IList{T}" /> used to store the <c>TKey</c>s in ordered manner.</typeparam>
	/// <typeparam name="TDictionary">
	///     The <see cref="IDictionary{TKey,TValue}" /> used to store the mapping between <c>TKey</c>
	///     :<c>TValue</c>.
	/// </typeparam>
	/// <seealso cref="OrderedDictionary{TKey,TValue}" />
	public abstract class OrderedDictionary<TKey, TValue, TList, TDictionary> : IOrderedDictionary<TKey, TValue>
		where TList : class, IList<TKey>, new() where TDictionary : class, IDictionary<TKey, TValue>, new() {
		protected OrderedDictionary() { }

		protected internal OrderedDictionary(in TDictionary initial) : this(in initial, []) { }

		protected internal OrderedDictionary(in TDictionary initial, in TList list) {
			Dictionary = initial;
			Indexes = list;

#pragma warning disable CA1062
			foreach (KeyValuePair<TKey, TValue> value in initial) {
#pragma warning restore CA1062
				Indexes.Add(value.Key);
			}
		}

		protected TDictionary Dictionary { get; } = new();
		protected TList Indexes { get; } = [];

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
			foreach (TKey key in Indexes) {
				bool res = Dictionary.TryGetValue(key, out TValue? value);
				Debug.Assert(res);

#pragma warning disable CS8604 // Possible null reference argument.
				yield return new KeyValuePair<TKey, TValue>(key, value);
#pragma warning restore CS8604 // Possible null reference argument.
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <inheritdoc />
		/// <summary>
		///     Append at the end a new <c>TKey</c>:<c>TValue</c> pair.
		/// </summary>
		/// <param name="item"></param>
		/// <exception cref="T:System.ArgumentException">when the <c>key</c> already exists.</exception>
		public void Add(KeyValuePair<TKey, TValue> item) => DoAdd(item.Key, item.Value);

		public void Clear() {
			Indexes.Clear();
			Dictionary.Clear();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item) => Contains(in item, EqualityComparer<TValue>.Default);

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
#pragma warning disable CA1062
			if ((arrayIndex < 0) || (arrayIndex > array.Length)) {
#pragma warning restore CA1062
				throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Out of bounds");
			}

			if (array.Length - arrayIndex < Indexes.Count) {
				throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Out of bounds");
			}

			int c = 0;

			foreach (TKey index in Indexes) {
				bool res = Dictionary.TryGetValue(index, out TValue? value);
				Debug.Assert(res);
#pragma warning disable CS8604 // Possible null reference argument.
				array[c + arrayIndex] = new KeyValuePair<TKey, TValue>(index, value);
#pragma warning restore CS8604 // Possible null reference argument.
				c++;
			}
		}

		public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

		public int Count => Indexes.Count;

		public bool IsReadOnly => Indexes.IsReadOnly;

		/// <inheritdoc />
		/// <summary>
		///     Append at the end a new <c>TKey</c>:<c>TValue</c> pair.
		/// </summary>
		/// <param name="key">the key to add.</param>
		/// <param name="value">the value to end.</param>
		/// <exception cref="T:System.ArgumentException">when the <c>key</c> already exists.</exception>
		public void Add(TKey key, TValue value) => DoAdd(in key, in value);

		public bool ContainsKey(TKey key) => Dictionary.ContainsKey(key);

		public bool Remove(TKey key) {
			if (Dictionary.Remove(key)) {
				bool removed = Indexes.Remove(key);

				if (!removed) {
					throw new InvalidOperationException();
				}

				return removed;
			}

			return false;
		}

#pragma warning disable CS8601 // Possible null reference assignment.
		public bool TryGetValue(TKey key, out TValue value) => Dictionary.TryGetValue(key, out value);
#pragma warning restore CS8601 // Possible null reference assignment.

		public TValue this[TKey key] {
			get => Dictionary[key];
			set => DoAdd(in key, in value, true);
		}

		public ICollection<TKey> Keys => new KeyCollection<OrderedDictionary<TKey, TValue, TList, TDictionary>>(this);

		public ICollection<TValue> Values => new ValuesCollection<OrderedDictionary<TKey, TValue, TList, TDictionary>>(this);

		public TValue this[int index] {
			get => At(index).Value;
			set => UpdateAt(index, value);
		}

		public void Insert(int index, in TKey key, in TValue value) {
			if (index == Indexes.Count) {
				Add(key, value);

				return;
			}

			CheckForOutOfBounds(index);

			if (ContainsKey(key)) {
				throw new ArgumentException("key already exists");
			}

			Indexes.Insert(index, key);
			DoUpdate(in key, in value, false);
		}

		public void RemoveAt(int index) {
			CheckForOutOfBounds(index);

			TKey key = Indexes[index];
			Indexes.RemoveAt(index);

			if (!Dictionary.Remove(key)) {
				throw new InvalidOperationException();
			}
		}

		public int IndexOf(in TKey key) => Indexes.IndexOf(key);

		public int IndexOf(in TValue value) => IndexOf(in value, EqualityComparer<TValue>.Default);

		public bool Contains<TEqualityComparer>(in KeyValuePair<TKey, TValue> item, TEqualityComparer comparer)
			where TEqualityComparer : IEqualityComparer<TValue> =>
			Dictionary.TryGetValue(item.Key, out TValue? value) && comparer.Equals(value, item.Value);

		public int IndexOf<TEqualityComparer>(in TValue value, TEqualityComparer comparer)
			where TEqualityComparer : IEqualityComparer<TValue> {
			int c = 0;

			foreach (KeyValuePair<TKey, TValue> pair in this) {
				if (comparer.Equals(pair.Value, value)) {
					return c;
				}

				c++;
			}

			return -1;
		}

		/// <summary>
		///     Access key-value pair at <paramref name="index">index</paramref> like an array.
		/// </summary>
		/// <param name="index"></param>
		/// <returns>the pair at <paramref name="index">index</paramref>.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index">index</paramref> is out of bounds.</exception>
		public KeyValuePair<TKey, TValue> At(int index) {
			CheckForOutOfBounds(index);
			TKey key = Indexes[index];

			return At(in key);
		}

		/// <summary>
		///     Access key-value pair at <paramref name="key">key</paramref> like a dictionary.
		/// </summary>
		/// <param name="key"></param>
		/// <returns>the pair identified by <paramref name="key">key</paramref>.</returns>
		/// <exception cref="KeyNotFoundException">when the <paramref name="key">key</paramref> doesn't exists.</exception>
		public KeyValuePair<TKey, TValue> At(in TKey key) {
			bool res = Dictionary.TryGetValue(key, out TValue? value);

			if (!res) {
				throw new KeyNotFoundException();
			}

#pragma warning disable CS8604 // Possible null reference argument.
			return new KeyValuePair<TKey, TValue>(key, value);
#pragma warning restore CS8604 // Possible null reference argument.
		}

		/// <summary>
		///     Update the <paramref name="value">value</paramref> for the given <paramref name="key">key.</paramref>
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns>the key.</returns>
		/// <exception cref="KeyNotFoundException">when the <paramref name="key">key</paramref> doesn't exists.</exception>
		public TKey UpdateAt(in TKey key, in TValue value) {
			DoUpdate(in key, in value);

			return key;
		}

		/// <summary>
		///     Update the <paramref name="value">value</paramref> at the given <paramref name="index">index.</paramref>
		/// </summary>
		/// <param name="index"></param>
		/// <param name="value"></param>
		/// <returns>the key.</returns>
		/// <exception cref="ArgumentOutOfRangeException">if index is out of bounds.</exception>
		public TKey UpdateAt(int index, in TValue value) {
			CheckForOutOfBounds(index);

			TKey key = Indexes[index];
			Debug.Assert(Dictionary.ContainsKey(key));
			DoUpdate(in key, in value, false);

			return key;
		}

		/// <summary>
		///     Swap the pair at the specified <paramref name="firstIndex">firstIndex</paramref> to the
		///     <paramref name="secondIndex">secondIndex</paramref> .
		/// </summary>
		/// <param name="firstIndex"></param>
		/// <param name="secondIndex"></param>
		/// <exception cref="ArgumentOutOfRangeException">one of the parameters if out of bounds</exception>
		public void Swap(int firstIndex, int secondIndex) {
			CheckForOutOfBounds(firstIndex);
			CheckForOutOfBounds(secondIndex);
			(Indexes[secondIndex], Indexes[firstIndex]) = (Indexes[firstIndex], Indexes[secondIndex]);
		}

		/// <summary>
		///     Reorder the pair at the specified <paramref name="fromIndex">fromIndex</paramref> to the
		///     <paramref name="toIndex">toIndex</paramref> .
		/// </summary>
		/// <param name="fromIndex">The zero-based index of the element to move.</param>
		/// <param name="toIndex">The zero-based index to move the element to.</param>
		/// <exception cref="ArgumentOutOfRangeException">one of the parameters if out of bounds</exception>
		public void Move(int fromIndex, int toIndex) {
			CheckForOutOfBounds(fromIndex);
			CheckForOutOfBounds(toIndex);

			if (fromIndex == toIndex) {
				return;
			}

			// This is a naive way for the best TList compatibility
			TKey tmp = Indexes[fromIndex];
			Indexes.RemoveAt(fromIndex);
			Indexes.Insert(toIndex, tmp);
			Debug.Assert(Dictionary.Count == Indexes.Count);
		}

		protected void DoUpdate(in TKey key, in TValue value, bool ensureExists = true) {
			if (ensureExists && !Dictionary.ContainsKey(key)) {
				throw new KeyNotFoundException();
			}

			Dictionary[key] = value;
		}

		protected void DoAdd(in TKey key, in TValue value, bool upsert = false) {
			if (Dictionary.ContainsKey(key)) {
				if (!upsert) {
					throw new ArgumentException("key already exists", nameof(key));
				}

				DoUpdate(in key, in value, false);

				return;
			}

			Indexes.Add(key);

			try {
				Dictionary.Add(key, value);
			}
			catch (Exception) {
				Indexes.RemoveAt(Indexes.Count - 1);

				throw;
			}
			finally {
				Debug.Assert(Dictionary.Count == Indexes.Count);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void CheckForOutOfBounds(int index, string paramName, string message = "") {
			Debug.Assert(Dictionary.Count == Indexes.Count);

			if ((uint) index > (uint) Indexes.Count) {
				throw new ArgumentOutOfRangeException(paramName, index, message);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void CheckForOutOfBounds(int index) => CheckForOutOfBounds(index, nameof(index));

		protected class KeyCollection<TDict> : ICollection<TKey>
			where TDict : OrderedDictionary<TKey, TValue, TList, TDictionary> {
			private readonly TDict Dictionary;

			protected internal KeyCollection(TDict dictionary) => Dictionary = dictionary;

			[MustDisposeResource]
			public IEnumerator<TKey> GetEnumerator() => Dictionary.Indexes.GetEnumerator();

			[MustDisposeResource]
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public void Add(TKey item) => throw new InvalidOperationException("readonly");

			public void Clear() => throw new InvalidOperationException("readonly");

			public bool Contains(TKey item) => Dictionary.Indexes.Contains(item);

			public void CopyTo(TKey[] array, int arrayIndex) {
#pragma warning disable CA1062
				if ((arrayIndex < 0) || (arrayIndex > array.Length)) {
#pragma warning restore CA1062
					throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Out of bounds");
				}

				if (array.Length - arrayIndex < Dictionary.Count) {
					throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Out of bounds");
				}

				Dictionary.Indexes.CopyTo(array, arrayIndex);
			}

			public bool Remove(TKey item) => throw new InvalidOperationException("readonly");

			public int Count => Dictionary.Indexes.Count;

			public bool IsReadOnly => true;
		}

		protected class ValuesCollection<TDict> : ICollection<TValue>
			where TDict : OrderedDictionary<TKey, TValue, TList, TDictionary> {
			protected private readonly TDict Dictionary;

			protected internal ValuesCollection(TDict dictionary) => Dictionary = dictionary;

			public IEnumerator<TValue> GetEnumerator() {
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (KeyValuePair<TKey, TValue> pair in Dictionary) {
					yield return pair.Value;
				}
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public void Add(TValue item) => throw new InvalidOperationException("readonly");

			public void Clear() => throw new InvalidOperationException("readonly");

			public bool Contains(TValue item) => Dictionary.Dictionary.Values.Contains(item);

			public void CopyTo(TValue[] array, int arrayIndex) {
#pragma warning disable CA1062
				if ((arrayIndex < 0) || (arrayIndex > array.Length)) {
#pragma warning restore CA1062
					throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Out of bounds");
				}

				if (array.Length - arrayIndex < Dictionary.Count) {
					throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Out of bounds");
				}

				int c = 0;

				foreach (TKey index in Dictionary.Indexes) {
					bool res = Dictionary.TryGetValue(index, out TValue value);
					Debug.Assert(res);
					array[c + arrayIndex] = value;
					c++;
				}
			}

			public bool Remove(TValue item) => throw new InvalidOperationException("readonly");

			public int Count => Dictionary.Count;

			public bool IsReadOnly => true;
		}
	}

	public class
		OrderedDictionary<TKey, TValue> : OrderedDictionary<TKey, TValue, List<TKey>, Dictionary<TKey, TValue>> where TKey : notnull {
		public OrderedDictionary() { }

		public OrderedDictionary(int capacity) : base(
			new Dictionary<TKey, TValue>(capacity),
			new List<TKey>(capacity)
		) { }

		public OrderedDictionary(IEqualityComparer<TKey> comparer) : base(new Dictionary<TKey, TValue>(comparer)) { }

		public OrderedDictionary(int capacity, IEqualityComparer<TKey> comparer) : base(new Dictionary<TKey, TValue>(capacity, comparer), new List<TKey>(capacity)) { }
	}
}
