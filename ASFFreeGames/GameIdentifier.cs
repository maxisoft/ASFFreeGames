using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Maxisoft.ASF;

// ReSharper disable once InconsistentNaming
[SuppressMessage("Design", "CA1051")]
public readonly struct GameIdentifier : IEquatable<GameIdentifier> {
	public readonly long Id;

	public readonly GameIdentifierType Type = GameIdentifierType.None;

	public GameIdentifier(long id = default, GameIdentifierType type = default) {
		Id = id;
		Type = type;
	}

	public bool Valid => (Id > 0) && Type is >= GameIdentifierType.None and <= GameIdentifierType.App;

	public static bool operator ==(GameIdentifier left, GameIdentifier right) => left.Equals(right);

	public static bool operator !=(GameIdentifier left, GameIdentifier right) => !left.Equals(right);

	public bool Equals(GameIdentifier other) => (Id == other.Id) && (Type == other.Type);

	public override bool Equals(object? obj) => obj is GameIdentifier other && Equals(other);

	public override int GetHashCode() => unchecked(((ulong) Id ^ BinaryPrimitives.ReverseEndianness((ulong) Type)).GetHashCode());

	[SuppressMessage("Design", "CA1065")]
	public override string ToString() =>
		Type switch {
			GameIdentifierType.None => Id.ToString(CultureInfo.InvariantCulture),
			GameIdentifierType.Sub => $"s/{Id}",
			GameIdentifierType.App => $"a/{Id}",
			_ => throw new ArgumentOutOfRangeException(nameof(Type))
		};

	public static bool TryParse([NotNull] string query, out GameIdentifier result) {
		ulong gameID;
		string type;
		GameIdentifierType identifierType;

		int index = query.IndexOf('/', StringComparison.Ordinal);

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

		switch (type.ToUpperInvariant()) {
			case "A":
			case "APP":
				identifierType = GameIdentifierType.App;

				break;
			case "S":
			case "SUB":
				identifierType = GameIdentifierType.Sub;

				break;
			default:
				identifierType = GameIdentifierType.None;

				break;
		}

		result = new GameIdentifier((long) gameID, identifierType);

		return true;
	}
}
