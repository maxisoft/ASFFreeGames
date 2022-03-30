using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArchiSteamFarm.Web.Responses;
using BloomFilter;
using JetBrains.Annotations;
using Maxisoft.Utils.Collections.Spans;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maxisoft.ASF.Reddit;

internal sealed class RedditHelper {
	private const string User = "ASFinfo";

	private static Uri GetUrl() => new Uri($"https://www.reddit.com/user/{User}.json?sort=new", UriKind.Absolute);

	private readonly Lazy<Regex> CommandRegex = new Lazy<Regex>(
		static () => new Regex(
			@"(.addlicense)\s+(asf)?\s*((?<appid>(s/|a/)\d+)\s*,?\s*)+.*?(?<free>permanently\s+free)?",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant
		)
	);

	private const int PoolMaxGameEntry = 1024;
	private static readonly ArrayPool<RedditGameEntry> ArrayPool = ArrayPool<RedditGameEntry>.Create(PoolMaxGameEntry, 1);
	private const int BloomFilterBufferSize = 8;
	private static readonly Lazy<float> BloomFilterK = new(static () => StringBloomFilterSpan.SolveK(BloomFilterBufferSize * BitSpan.LongNumBit, 1e-2));

	private RedditGameEntry[] LoadMessages(JToken children) {
		Regex regex = CommandRegex.Value;
		RedditGameEntry[] buffer = ArrayPool.Rent(PoolMaxGameEntry / 2);
		Span<long> bloomFilterBuffer = stackalloc long[BloomFilterBufferSize];
		StringBloomFilterSpan bloomFilter = new(bloomFilterBuffer, BloomFilterK.Value);

		try {
			SpanList<RedditGameEntry> list = new(buffer);

			foreach (var comment in children.Children<JObject>()) {
				JToken? commentData = comment.GetValue("data", StringComparison.InvariantCulture);
				var text = commentData?.Value<string>("body") ?? string.Empty;
				var date = commentData?.Value<long?>("created_utc") ?? commentData?.Value<long?>("created") ?? 0;
				var match = regex.Match(text);

				if (!match.Success) {
					continue;
				}

				bool freeToPlay = match.Groups["free"].Success;
				RedditGameEntry gameEntry;

				foreach (Group matchGroup in match.Groups) {
					if (matchGroup.Name.StartsWith("appid", StringComparison.InvariantCulture)) {
						gameEntry = new RedditGameEntry(matchGroup.Value, freeToPlay, date);

						if (bloomFilter.Contains(gameEntry.Identifier)) {
							// remove potential duplicates
							list.Remove(in gameEntry);
						}

						list.Add(in gameEntry);
						bloomFilter.Add(gameEntry.Identifier);

						while (list.Count >= list.Capacity) {
							// should not append but better safe than sorry
							list.RemoveAt(0);
						}
					}
				}
			}

			RedditGameEntry[] res = list.ToArray();
			Array.Sort(res, new RedditGameEntryComparerOnDate());

			return res;
		}
		finally {
			ArrayPool.Return(buffer);
		}
	}

	public async ValueTask<ICollection<RedditGameEntry>> ListGames() {
		var webBrowser = ArchiSteamFarm.Core.ASF.WebBrowser;
		var res = new List<RedditGameEntry>();

		if (webBrowser is null) {
			return res;
		}

		ObjectResponse<JToken>? payload;

		try {
			payload = await webBrowser.UrlGetToJsonObject<JToken>(GetUrl()).ConfigureAwait(false);
		}
		catch (Exception e) when (e is JsonException or IOException) {
			return res;
		}

		if (payload is null) {
			return res;
		}

		if ((payload.Content.Value<string>("kind") ?? string.Empty) != "Listing") {
			return res;
		}

		var data = payload.Content.Value<JObject>("data");

		// ReSharper disable once ConditionIsAlwaysTrueOrFalse
		if (data is null || !data.TryGetValue("children", out var children) || children is null) {
			return res;
		}

		return LoadMessages(children);
	}
}
