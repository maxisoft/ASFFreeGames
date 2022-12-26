using System;
using System.IO;
using System.Reflection;
using Maxisoft.ASF.Reddit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ASFFreeGames.Tests.Reddit;

public sealed class RedditHelperTests {
	private static readonly Lazy<JToken> ASFinfo = new(LoadAsfinfoJson);
	private readonly RedditHelper RedditHelper = new();

	[Fact]
	public void TestNotEmpty() {
		JToken payload = ASFinfo.Value;
		RedditGameEntry[] entries = RedditHelper.LoadMessages(payload.Value<JObject>("data")!["children"]!);
		Assert.NotEmpty(entries);
	}

	[Theory]
	[InlineData("s/762440")]
	[InlineData("a/1601550")]
	public void TestContains(string appid) {
		JToken payload = ASFinfo.Value;
		RedditGameEntry[] entries = RedditHelper.LoadMessages(payload.Value<JObject>("data")!["children"]!);
		Assert.Contains(new RedditGameEntry(appid, false, long.MaxValue), entries, new GameEntryIdentifierEqualityComparer());
	}

	[Fact]
	public void TestMaintainOrder() {
		JToken payload = ASFinfo.Value;
		RedditGameEntry[] entries = RedditHelper.LoadMessages(payload.Value<JObject>("data")!["children"]!);
		int app762440 = Array.FindIndex(entries, static entry => entry.Identifier == "s/762440");
		int app1601550 = Array.FindIndex(entries, static entry => entry.Identifier == "a/1601550");
		Assert.InRange(app762440, 0, long.MaxValue);
		Assert.InRange(app1601550, checked(app762440 + 1), long.MaxValue); // app1601550 is after app762440

		int app1631250 = Array.FindIndex(entries, static entry => entry.Identifier == "a/1631250");
		Assert.InRange(app1631250, checked(app1601550 + 1), long.MaxValue); // app1631250 is after app1601550
		Assert.Equal(entries.Length - 1, app1631250);
	}

	[Fact]
	public void TestFreeToPlayParsing() {
		JToken payload = ASFinfo.Value;
		RedditGameEntry[] entries = RedditHelper.LoadMessages(payload.Value<JObject>("data")!["children"]!);
		RedditGameEntry f2pEntry = Array.Find(entries, static entry => entry.Identifier == "a/1631250");
		Assert.True(f2pEntry.FreeToPlay);

		RedditGameEntry getEntry(string identifier) => Array.Find(entries, entry => entry.Identifier == identifier);

		f2pEntry = getEntry("a/431650"); // F2P
		Assert.True(f2pEntry.FreeToPlay);

		f2pEntry = getEntry("a/579730");
		Assert.True(f2pEntry.FreeToPlay);

		RedditGameEntry dlcEntry = getEntry("s/791643"); // DLC
		Assert.False(dlcEntry.FreeToPlay);

		dlcEntry = getEntry("s/791642");
		Assert.False(dlcEntry.FreeToPlay);

		RedditGameEntry paidEntry = getEntry("s/762440"); // Warhammer: Vermintide 2
		Assert.False(paidEntry.FreeToPlay);

		paidEntry = getEntry("a/1601550");
		Assert.False(paidEntry.FreeToPlay);
	}

	private static JToken LoadAsfinfoJson() {
		Assembly assembly = Assembly.GetExecutingAssembly();

		using Stream stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.ASFinfo.json")!;

		using StreamReader reader = new(stream);
		using JsonTextReader jsonTextReader = new(reader);

		return JToken.Load(jsonTextReader);
	}
}
