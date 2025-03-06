using System;
using System.Diagnostics.CodeAnalysis;

namespace ASFFreeGames.ASFExtensions.Bot {
	/// <summary>
	/// Represents a readonly record struct that encapsulates bot's name (a string) and provides implicit conversion and comparison methods.
	/// </summary>

	// ReSharper disable once InheritdocConsiderUsage
	public readonly record struct BotName(string Name) : IComparable<BotName> {
		// The culture-invariant comparer for string comparison
		private static readonly StringComparer Comparer = StringComparer.InvariantCultureIgnoreCase;

		/// <summary>
		/// Converts a <see cref="BotName"/> instance to a <see cref="string"/> implicitly.
		/// </summary>
		public static implicit operator string(BotName botName) => botName.Name;

		/// <summary>
		/// Converts a <see cref="string"/> to a <see cref="BotName"/> instance implicitly.
		/// </summary>
		[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "The constructor serves as an alternative method.")]
		public static implicit operator BotName(string value) => new BotName(value);

		/// <summary>
		/// Returns the string representation of this instance.
		/// </summary>
		public override string ToString() => Name;

		/// <inheritdoc/>
		public bool Equals(BotName other) => Comparer.Equals(Name, other.Name);

		/// <inheritdoc/>
		public override int GetHashCode() => Comparer.GetHashCode(Name);

		/// <inheritdoc/>
		public int CompareTo(BotName other) => Comparer.Compare(Name, other.Name);

		// Implement the relational operators using the CompareTo method
		public static bool operator <(BotName left, BotName right) => left.CompareTo(right) < 0;
		public static bool operator <=(BotName left, BotName right) => left.CompareTo(right) <= 0;
		public static bool operator >(BotName left, BotName right) => left.CompareTo(right) > 0;
		public static bool operator >=(BotName left, BotName right) => left.CompareTo(right) >= 0;
	}
}
