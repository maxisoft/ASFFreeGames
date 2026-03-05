using System;
using System.Diagnostics.CodeAnalysis;
using ASFFreeGames.ASFExtensions.Games;

namespace Maxisoft.ASF.ASFExtensions.Games;

/// <summary>
///     Represents a static class that provides methods for parsing game identifiers from strings.
/// </summary>
internal static class GameIdentifierParser {
	/// <summary>
	///     Tries to parse a game identifier from a query string.
	/// </summary>
	/// <param name="query">The query string to parse.</param>
	/// <param name="result">The resulting game identifier if the parsing was successful.</param>
	/// <returns>True if the parsing was successful; otherwise, false.</returns>
	public static bool TryParse(ReadOnlySpan<char> query, out GameIdentifier result) {
		if (query.IsEmpty) // Check for empty query first
		{
			result = default(GameIdentifier);

			return false;
		}

		ulong gameID;
		ReadOnlySpan<char> type;
		GameIdentifierType identifierType = GameIdentifierType.None;

		int index = query.IndexOf('/');

		if ((index > 0) && (query.Length > index + 1)) {
			if (!ulong.TryParse(query[(index + 1)..], out gameID) || (gameID == 0)) {
				result = default(GameIdentifier);

				return false;
			}

			type = query[..index];
		}
		else if (ulong.TryParse(query, out gameID) && (gameID > 0)) {
			type = "SUB";
		}
		else {
			result = default(GameIdentifier);

			return false;
		}

		if (type.Length > 3) {
			type = type[..3];
		}

		if (type.Length == 1) {
			char c = char.ToUpperInvariant(type[0]);

			identifierType = c switch {
				'A' => GameIdentifierType.App,
				'S' => GameIdentifierType.Sub,
				_ => identifierType,
			};
		}

		if (identifierType is GameIdentifierType.None) {
			switch (type.Length) {
				case 0:
					break;
				case 1 when char.ToUpperInvariant(type[0]) == 'A':
				case 3
					when (char.ToUpperInvariant(type[0]) == 'A')
						&& (char.ToUpperInvariant(type[1]) == 'P')
						&& (char.ToUpperInvariant(type[2]) == 'P'):
					identifierType = GameIdentifierType.App;

					break;
				case 1 when char.ToUpperInvariant(type[0]) == 'S':
				case 3
					when (char.ToUpperInvariant(type[0]) == 'S')
						&& (char.ToUpperInvariant(type[1]) == 'U')
						&& (char.ToUpperInvariant(type[2]) == 'B'):
					identifierType = GameIdentifierType.Sub;

					break;
				default:
					result = default(GameIdentifier);

					return false;
			}
		}

		result = new GameIdentifier((long) gameID, identifierType);

		return result.Valid;
	}
}
