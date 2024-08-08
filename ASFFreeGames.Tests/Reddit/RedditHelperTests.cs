using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Maxisoft.ASF.Reddit;
using Maxisoft.Utils.Collections.Spans;
using Xunit;

namespace Maxisoft.ASF.Tests.Reddit;

public sealed class RedditHelperTests {
	[Fact]
	public async Task TestNotEmpty() {
		RedditGameEntry[] entries = await LoadAsfinfoEntries().ConfigureAwait(false);
		Assert.NotEmpty(entries);
	}

	[Theory]
	[InlineData("s/762440")]
	[InlineData("a/1601550")]
	public async Task TestContains(string appid) {
		RedditGameEntry[] entries = await LoadAsfinfoEntries().ConfigureAwait(false);
		Assert.Contains(new RedditGameEntry(appid, default(ERedditGameEntryKind), long.MaxValue), entries, new GameEntryIdentifierEqualityComparer());
	}

	[Fact]
	public async Task TestMaintainOrder() {
		RedditGameEntry[] entries = await LoadAsfinfoEntries().ConfigureAwait(false);
		int app762440 = Array.FindIndex(entries, static entry => entry.Identifier == "s/762440");
		int app1601550 = Array.FindIndex(entries, static entry => entry.Identifier == "a/1601550");
		Assert.InRange(app762440, 0, long.MaxValue);
		Assert.InRange(app1601550, checked(app762440 + 1), long.MaxValue); // app1601550 is after app762440

		int app1631250 = Array.FindIndex(entries, static entry => entry.Identifier == "a/1631250");
		Assert.InRange(app1631250, checked(app1601550 + 1), long.MaxValue); // app1631250 is after app1601550
		Assert.Equal(entries.Length - 1, app1631250);
	}

	[Fact]
	public async Task TestFreeToPlayParsing() {
		RedditGameEntry[] entries = await LoadAsfinfoEntries().ConfigureAwait(false);
		RedditGameEntry f2pEntry = Array.Find(entries, static entry => entry.Identifier == "a/1631250");
		Assert.True(f2pEntry.IsFreeToPlay);

		RedditGameEntry getEntry(string identifier) => Array.Find(entries, entry => entry.Identifier == identifier);

		f2pEntry = getEntry("a/431650"); // F2P
		Assert.True(f2pEntry.IsFreeToPlay);

		f2pEntry = getEntry("a/579730");
		Assert.True(f2pEntry.IsFreeToPlay);

		RedditGameEntry dlcEntry = getEntry("s/791643"); // DLC
		Assert.False(dlcEntry.IsFreeToPlay);

		dlcEntry = getEntry("s/791642");
		Assert.False(dlcEntry.IsFreeToPlay);

		RedditGameEntry paidEntry = getEntry("s/762440"); // Warhammer: Vermintide 2
		Assert.False(paidEntry.IsFreeToPlay);

		paidEntry = getEntry("a/1601550");
		Assert.False(paidEntry.IsFreeToPlay);
	}

	[Fact]
	public async Task TestDlcParsing() {
		RedditGameEntry[] entries = await LoadAsfinfoEntries().ConfigureAwait(false);
		RedditGameEntry f2pEntry = Array.Find(entries, static entry => entry.Identifier == "a/1631250");
		Assert.False(f2pEntry.IsForDlc);

		RedditGameEntry getEntry(string identifier) => Array.Find(entries, entry => entry.Identifier == identifier);

		f2pEntry = getEntry("a/431650"); // F2P
		Assert.False(f2pEntry.IsForDlc);

		f2pEntry = getEntry("a/579730");
		Assert.False(f2pEntry.IsForDlc);

		RedditGameEntry dlcEntry = getEntry("s/791643"); // DLC
		Assert.True(dlcEntry.IsForDlc);

		dlcEntry = getEntry("s/791642");
		Assert.True(dlcEntry.IsForDlc);

		RedditGameEntry paidEntry = getEntry("s/762440"); // Warhammer: Vermintide 2
		Assert.False(paidEntry.IsForDlc);

		paidEntry = getEntry("a/1601550");
		Assert.False(paidEntry.IsForDlc);
	}

	private static async Task<RedditGameEntry[]> LoadAsfinfoEntries() {
		Assembly assembly = Assembly.GetExecutingAssembly();

#pragma warning disable CA2007
		await using Stream stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.ASFinfo.json")!;
#pragma warning restore CA2007
		JsonNode jsonNode = await JsonNode.ParseAsync(stream).ConfigureAwait(false) ?? JsonNode.Parse("{}")!;

		return RedditHelper.LoadMessages(jsonNode["data"]?["children"]!).ToArray();
	}
}
