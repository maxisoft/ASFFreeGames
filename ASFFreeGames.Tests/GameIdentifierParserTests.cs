using System;
using Xunit;

namespace Maxisoft.ASF.Tests;

// A test class for the GameIdentifierParser class
public sealed class GameIdentifierParserTests {
	// A test method that checks if the TryParse method can handle invalid game identifiers
	[Theory]
	[InlineData("a/-1")] // Negative AppID
	[InlineData("s/0")] // Zero SubID
	[InlineData("x/123")] // Unknown type prefix
	[InlineData("app/foo")] // Non-numeric ID
	[InlineData("")] // Empty query
	[InlineData("/")] // Missing ID
	[InlineData("a/")] // Missing AppID
	[InlineData("s/")] // Missing SubID
	public void TryParse_InvalidGameIdentifiers_ReturnsFalseAndDefaultResult(string query) {
		// Arrange
		// The default result for invalid queries
		GameIdentifier defaultResult = default;

		// Act and Assert
		Assert.False(GameIdentifierParser.TryParse(query, out _)); // Parsing should return false
	}

	// A test method that checks if the TryParse method can parse valid game identifiers
	[Theory]
	[InlineData("a/730", 730, GameIdentifierType.App)] // AppID 730 (Counter-Strike: Global Offensive)
	[InlineData("s/303386", 303386, GameIdentifierType.Sub)] // SubID 303386 (Humble Monthly - May 2017)
	[InlineData("app/570", 570, GameIdentifierType.App)] // AppID 570 (Dota 2)
	[InlineData("sub/29197", 29197, GameIdentifierType.Sub)] // SubID 29197 (Portal Bundle)
	[InlineData("570", 570, GameIdentifierType.Sub)] // AppID 570 (Dota 2), default type is Sub
	[InlineData("A/440", 440, GameIdentifierType.App)] // AppID 440 (Team Fortress 2)
	[InlineData("APP/218620", 218620, GameIdentifierType.App)] // AppID 218620 (PAYDAY 2)
	[InlineData("S/29197", 29197, GameIdentifierType.Sub)] // SubID 29197 (Portal Bundle)
	public void TryParse_ValidGameIdentifiers_ReturnsTrueAndCorrectResult(string query, long id, GameIdentifierType type) {
		// Arrange
		// The expected result for the query
		GameIdentifier expectedResult = new(id, type);

		// Act and Assert
		Assert.True(GameIdentifierParser.TryParse(query, out GameIdentifier result)); // Parsing should return true
		Assert.Equal(expectedResult, result); // Result should match the expected result
	}
}
