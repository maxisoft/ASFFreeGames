using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#pragma warning disable CS0660, CS0661

namespace Maxisoft.Utils.Collections {
	/// <summary>
	/// </summary>
	/// <remarks>Prefer .NET Standard 2.1 <see cref="System.Index" /> when available</remarks>
	[DebuggerDisplay("{" + nameof(Value) + "}")]
	[SuppressMessage("Design", "CA1051")]
	[SuppressMessage("Performance", "CA1815")]
	public readonly struct WrappedIndex {
		public readonly int Value;

		public WrappedIndex(int value) {
			Value = value;
		}

		[SuppressMessage("Usage", "CA2225")]
		public static implicit operator WrappedIndex(int value) {
			return new WrappedIndex(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Resolve(int size) {
			return Value >= 0 ? Value : size + Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Resolve<T, TCollection>([NotNull] in ICollection<T> collection)
			where TCollection : ICollection<T> {
			return Resolve(collection.Count);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Resolve<T>(in ICollection<T> collection) {
			return Resolve<T, ICollection<T>>(collection);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Resolve<TCollection>(in TCollection collection)
			where TCollection : ICollection {
			return Resolve(collection.Count);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Resolve([NotNull] in Array array) {
			return Resolve(array.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Resolve<T>([NotNull] in T[] array) {
			return Resolve(array.Length);
		}

		public bool Equals(WrappedIndex other) => Value == other.Value;

		public override int GetHashCode() => Value.GetHashCode();

		public static bool operator ==(WrappedIndex left, WrappedIndex right) => left.Equals(right);

		public static bool operator !=(WrappedIndex left, WrappedIndex right) => !(left == right);
	}
}
