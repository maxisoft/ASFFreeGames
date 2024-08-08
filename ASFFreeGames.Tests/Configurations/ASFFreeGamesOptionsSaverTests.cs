using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using ASFFreeGames.Configurations;
using Xunit;

namespace Maxisoft.ASF.Tests.Configurations;

public class ASFFreeGamesOptionsSaverTests {
	[Fact]
#pragma warning disable CA1707
	public async void SaveOptions_WritesValidJson_And_ParsesCorrectly() {
#pragma warning restore CA1707

		// Arrange
		ASFFreeGamesOptions options = new() {
			RecheckInterval = TimeSpan.FromHours(1),
			RandomizeRecheckInterval = true,
			SkipFreeToPlay = false,
			SkipDLC = true,
			Blacklist = new HashSet<string> {
				"game1",
				"game2",
				"a gamewith2xquote(\")'",
				"game with strange char €$çêà /\\\n\r\t"
			},
			VerboseLog = null,
			Proxy = "http://localhost:1080",
			RedditProxy = "socks5://192.168.1.1:1081"
		};

		using MemoryStream memoryStream = new();

		// Act
		_ = await ASFFreeGamesOptionsSaver.SaveOptions(memoryStream, options).ConfigureAwait(false);

		// Assert - Validate UTF-8 encoding
		memoryStream.Position = 0;
		Assert.NotEmpty(Encoding.UTF8.GetString(memoryStream.ToArray()));

		// Assert - Parse JSON and access properties
		memoryStream.Position = 0;
		string json = Encoding.UTF8.GetString(memoryStream.ToArray());
		ASFFreeGamesOptions? deserializedOptions = JsonSerializer.Deserialize<ASFFreeGamesOptions>(json);

		Assert.NotNull(deserializedOptions);
		Assert.Equal(options.RecheckInterval, deserializedOptions.RecheckInterval);
		Assert.Equal(options.RandomizeRecheckInterval, deserializedOptions.RandomizeRecheckInterval);
		Assert.Equal(options.SkipFreeToPlay, deserializedOptions.SkipFreeToPlay);
		Assert.Equal(options.SkipDLC, deserializedOptions.SkipDLC);
		Assert.Equal(options.Blacklist, deserializedOptions.Blacklist);
		Assert.Equal(options.VerboseLog, deserializedOptions.VerboseLog);
		Assert.Equal(options.Proxy, deserializedOptions.Proxy);
		Assert.Equal(options.RedditProxy, deserializedOptions.RedditProxy);
	}
}
