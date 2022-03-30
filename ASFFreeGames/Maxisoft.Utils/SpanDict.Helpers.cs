using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Maxisoft.Utils.Collections.Spans {
	public ref partial struct SpanDict<TKey, TValue> where TKey : notnull {
		public readonly KeyValuePair<TKey, TValue>[] ToArray() {
			var array = new KeyValuePair<TKey, TValue>[Count];
			CopyTo(array, 0);

			return array;
		}

		public readonly TDictionary ToDictionary<TDictionary>() where TDictionary : IDictionary<TKey, TValue>, new() {
			var dict = new TDictionary();

			foreach (var pair in this) {
				dict.Add(pair);
			}

			return dict;
		}

		public readonly Dictionary<TKey, TValue> ToDictionary() {
			var dict = new Dictionary<TKey, TValue>(Count);

			foreach (var pair in this) {
				dict.Add(pair.Key, pair.Value);
			}

			return dict;
		}

		[DebuggerNonUserCode]
		private readonly ref struct DebuggerTypeProxyImpl {
			private readonly SpanDict<TKey, TValue> _dict;

			public DebuggerTypeProxyImpl(SpanDict<TKey, TValue> dict) {
				_dict = dict;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
			public long Count => _dict.Count;

			[DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
			public long Capacity => _dict.Capacity;

			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public BitSpan Mask => _dict.Mask;

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public KeyValuePair<TKey, TValue>[] Items => _dict.ToArray();
		}

		[SuppressMessage("Design", "CA1034")]
		public ref struct Enumerator {
			/// <summary>The span being enumerated.</summary>
			private readonly SpanDict<TKey, TValue> _dict;

			/// <summary>The next index to yield.</summary>
			private int _index;

			/// <summary>Initialize the enumerator.</summary>
			/// <param name="dict">The dict to enumerate.</param>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(SpanDict<TKey, TValue> dict) {
				_dict = dict;
				_index = -1;
			}

			/// <summary>Advances the enumerator to the next element of the dict.</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext() {
				var index = _index + 1;

				while (index < _dict.Capacity && !_dict.Mask[index]) {
					index += 1;
				}

				if (index >= _dict.Capacity) {
					return false;
				}

				_index = index;

				return true;
			}

			/// <summary>Gets the element at the current position of the enumerator.</summary>
			public ref KeyValuePair<TKey, TValue> Current {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => ref _dict.Buckets[_index];
			}
		}

		[SuppressMessage("Design", "CA1034")]
		public ref struct KeyEnumerator {
			/// <summary>The span being enumerated.</summary>
			private readonly SpanDict<TKey, TValue> _dict;

			/// <summary>The next index to yield.</summary>
			private int _index;

			/// <summary>Initialize the enumerator.</summary>
			/// <param name="dict">The dict to enumerate.</param>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal KeyEnumerator(SpanDict<TKey, TValue> dict) {
				_dict = dict;
				_index = -1;
			}

			public int Count => _dict.Count;

			/// <summary>Advances the enumerator to the next element of the dict.</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext() {
				var index = _index + 1;

				while (index < _dict.Capacity && !_dict.Mask[index]) {
					index += 1;
				}

				if (index >= _dict.Capacity) {
					return false;
				}

				_index = index;

				return true;
			}

			/// <summary>Gets the element at the current position of the enumerator.</summary>
			public TKey Current {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => _dict.Buckets[_index].Key;
			}

			public KeyEnumerator GetEnumerator() {
				return this;
			}
		}

		[SuppressMessage("Design", "CA1034")]
		public ref struct ValueEnumerator {
			/// <summary>The span being enumerated.</summary>
			private readonly SpanDict<TKey, TValue> _dict;

			/// <summary>The next index to yield.</summary>
			private int _index;

			/// <summary>Initialize the enumerator.</summary>
			/// <param name="dict">The dict to enumerate.</param>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal ValueEnumerator(SpanDict<TKey, TValue> dict) {
				_dict = dict;
				_index = -1;
			}

			public int Count => _dict.Count;

			public ValueEnumerator GetEnumerator() {
				return this;
			}

			/// <summary>Advances the enumerator to the next element of the dict.</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext() {
				var index = _index + 1;

				while (index < _dict.Capacity && !_dict.Mask[index]) {
					index += 1;
				}

				if (index >= _dict.Capacity) {
					return false;
				}

				_index = index;

				return true;
			}

			/// <summary>Gets the element at the current position of the enumerator.</summary>
			public TValue Current {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => _dict.Buckets[_index].Value;
			}
		}
	}
}
