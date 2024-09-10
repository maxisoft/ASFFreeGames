using System.Collections.Generic;

namespace Maxisoft.Utils.Collections.Dictionaries {
	public interface IOrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue> {
		public TValue this[int index] { get; set; }
		public void Insert(int index, in TKey key, in TValue value);
		public void RemoveAt(int index);

		public int IndexOf(in TKey key);

		public int IndexOf(in TValue value);
	}
}
