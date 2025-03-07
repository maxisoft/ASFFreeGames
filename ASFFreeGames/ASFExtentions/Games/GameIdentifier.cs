using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Maxisoft.ASF.ASFExtensions;
using Maxisoft.ASF.ASFExtensions.Games;

// ReSharper disable RedundantNullableFlowAttribute

namespace ASFFreeGames.ASFExtensions.Games;

/// <summary>
///     Represents a readonly record struct that encapsulates a game identifier with a numeric ID and a type.
/// </summary>
public readonly record struct GameIdentifier(
	long Id,
	GameIdentifierType Type = GameIdentifierType.None
) {
	/// <summary>
	///     Gets a value indicating whether the game identifier is valid.
	/// </summary>
	public bool Valid =>
		(Id > 0) && Type is >= GameIdentifierType.None and <= GameIdentifierType.App;

	public override int GetHashCode() =>
		unchecked(((ulong) Id ^ BinaryPrimitives.ReverseEndianness((ulong) Type)).GetHashCode());

	/// <summary>
	///     Returns the string representation of the game identifier.
	/// </summary>
	[SuppressMessage("Design", "CA1065")]
	public override string ToString() =>
		Type switch {
			GameIdentifierType.None => Id.ToString(CultureInfo.InvariantCulture),
			GameIdentifierType.Sub => $"s/{Id}",
			GameIdentifierType.App => $"a/{Id}",
			_ => throw new ArgumentOutOfRangeException(nameof(Type)),
		};

	/// <summary>
	///     Tries to parse a game identifier from a query string.
	/// </summary>
	/// <param name="query">The query string to parse.</param>
	/// <param name="result">The resulting game identifier if the parsing was successful.</param>
	/// <returns>True if the parsing was successful; otherwise, false.</returns>
	public static bool TryParse([NotNull] ReadOnlySpan<char> query, out GameIdentifier result) =>
		GameIdentifierParser.TryParse(query, out result);
}
