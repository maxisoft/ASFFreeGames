using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

	internal RedditGameEntry[] LoadMessages(JToken children) {
		Regex regex = CommandRegex.Value;
		RedditGameEntry[] buffer = ArrayPool.Rent(PoolMaxGameEntry / 2);
		Span<long> bloomFilterBuffer = stackalloc long[BloomFilterBufferSize];
		StringBloomFilterSpan bloomFilter = new(bloomFilterBuffer, 3);

		try {
			SpanList<RedditGameEntry> list = new(buffer);

			foreach (var comment in children.Children<JObject>()) {
				JToken? commentData = comment.GetValue("data", StringComparison.InvariantCulture);
				var text = commentData?.Value<string>("body") ?? string.Empty;
				var date = commentData?.Value<long?>("created_utc") ?? commentData?.Value<long?>("created") ?? 0;
				var matches = regex.Matches(text);

				foreach (Match match in matches) {
					bool freeToPlay = match.Groups["free"].Success;
					RedditGameEntry gameEntry;

					foreach (Group matchGroup in match.Groups) {
						if (!matchGroup.Name.StartsWith("appid", StringComparison.InvariantCulture)) {
							continue;
						}

						foreach (Capture capture in matchGroup.Captures) {
							gameEntry = new RedditGameEntry(capture.Value, freeToPlay, date);

							int index = -1;

							if (bloomFilter.Contains(gameEntry.Identifier)) {
								index = list.IndexOf(gameEntry, new GameEntryIdentifierEqualityComparer());
							}

							if (index >= 0) {
								list[index] = gameEntry;
							}
							else {
								list.Add(in gameEntry);
								bloomFilter.Add(gameEntry.Identifier);
							}

							while (list.Count >= list.Capacity) {
								// should not append but better safe than sorry
								list.RemoveAt(list.Count - 1);
							}
						}
					}
				}
			}

			RedditGameEntry[] res = list.ToArray();

			return res;
		}
		finally {
			ArrayPool.Return(buffer);
		}
	}

	public async ValueTask<ICollection<RedditGameEntry>> ListGames() {
		var webBrowser = ArchiSteamFarm.Core.ASF.WebBrowser;
		var res = Array.Empty<RedditGameEntry>();

		if (webBrowser is null) {
			return res;
		}

		ObjectResponse<JToken>? payload;

		try {
			payload = await webBrowser.UrlGetToJsonObject<JToken>(GetUrl(), rateLimitingDelay: 500).ConfigureAwait(false);
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
