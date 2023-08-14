using System;
using Xunit;

namespace Maxisoft.ASF.Tests;

#pragma warning disable CA1707 // Identifiers should not contain underscores

// A test class for the GameIdentifier struct
public sealed class GameIdentifierTests {
	// A test method that checks if the Valid property returns true for valid game identifiers
	[Theory]
	[InlineData(730, GameIdentifierType.App)] // AppID 730 (Counter-Strike: Global Offensive)
	[InlineData(303386, GameIdentifierType.Sub)] // SubID 303386 (Humble Monthly - May 2017)
	[InlineData(570, GameIdentifierType.App)] // AppID 570 (Dota 2)
	[InlineData(29197, GameIdentifierType.Sub)] // SubID 29197 (Portal Bundle)
	public void Valid_ReturnsTrueForValidGameIdentifiers(long id, GameIdentifierType type) {
		// Arrange
		// Create a game identifier with the given id and type
		GameIdentifier gameIdentifier = new(id, type);

		// Act and Assert
		Assert.True(gameIdentifier.Valid); // The Valid property should return true
	}

	// A test method that checks if the Valid property returns false for invalid game identifiers
	[Theory]
	[InlineData(-1, GameIdentifierType.App)] // Negative AppID
	[InlineData(0, GameIdentifierType.Sub)] // Zero SubID
	[InlineData(456, (GameIdentifierType) 4)] // Invalid type value
	public void Valid_ReturnsFalseForInvalidGameIdentifiers(long id, GameIdentifierType type) {
		// Arrange
		// Create a game identifier with the given id and type
		GameIdentifier gameIdentifier = new(id, type);

		// Act and Assert
		Assert.False(gameIdentifier.Valid); // The Valid property should return false
	}

	// A test method that checks if the ToString method returns the correct string representation of the game identifier
	[Theory]
	[InlineData(730, GameIdentifierType.App, "a/730")] // AppID 730 (Counter-Strike: Global Offensive)
	[InlineData(303386, GameIdentifierType.Sub, "s/303386")] // SubID 303386 (Humble Monthly - May 2017)
	[InlineData(570, GameIdentifierType.None, "570")] // AppID 570 (Dota 2), no type specified
	public void ToString_ReturnsCorrectStringRepresentation(long id, GameIdentifierType type, string expectedString) {
		// Arrange
		// Create a game identifier with the given id and type
		GameIdentifier gameIdentifier = new(id, type);

		// Act and Assert
		Assert.Equal(expectedString, gameIdentifier.ToString()); // The ToString method should return the expected string
	}

	// A test method that checks if the GetHashCode method returns the same value for equal game identifiers
	[Theory]
	[InlineData(730, GameIdentifierType.App)] // AppID 730 (Counter-Strike: Global Offensive)
	[InlineData(303386, GameIdentifierType.Sub)] // SubID 303386 (Humble Monthly - May 2017)
	[InlineData(570, GameIdentifierType.None)] // AppID 570 (Dota 2), no type specified
	public void GetHashCode_ReturnsSameValueForEqualGameIdentifiers(long id, GameIdentifierType type) {
		// Arrange
		// Create two equal game identifiers with the given id and type
		GameIdentifier gameIdentifier1 = new(id, type);
		GameIdentifier gameIdentifier2 = new(id, type);

		// Act and Assert
		Assert.Equal(gameIdentifier1.GetHashCode(), gameIdentifier2.GetHashCode()); // The GetHashCode method should return the same value for both game identifiers
	}

	// A test method that checks if the GetHashCode method returns different values for unequal game identifiers
	[Theory]
	[InlineData(730, GameIdentifierType.App, 731, GameIdentifierType.App)] // Different AppIDs with same type
	[InlineData(303386, GameIdentifierType.Sub, 303387, GameIdentifierType.Sub)] // Different SubIDs with same type
	[InlineData(570, GameIdentifierType.App, 570, GameIdentifierType.Sub)] // Same ID with different types
	public void GetHashCode_ReturnsDifferentValueForUnequalGameIdentifiers(long id1, GameIdentifierType type1, long id2, GameIdentifierType type2) {
		// Arrange
		// Create two unequal game identifiers with the given ids and types
		GameIdentifier gameIdentifier1 = new(id1, type1);
		GameIdentifier gameIdentifier2 = new(id2, type2);

		// Act and Assert
		Assert.NotEqual(gameIdentifier1.GetHashCode(), gameIdentifier2.GetHashCode()); // The GetHashCode method should return different values for both game identifiers
	}
}

#pragma warning restore CA1707
