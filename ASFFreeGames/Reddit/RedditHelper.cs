using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Web;
using ArchiSteamFarm.Web.Responses;
using BloomFilter;
using Maxisoft.Utils.Collections.Spans;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maxisoft.ASF.Reddit;

internal sealed partial class RedditHelper {
	private const int BloomFilterBufferSize = 8;

	private const int PoolMaxGameEntry = 1024;
	private const string User = "ASFinfo";
	private static readonly ArrayPool<RedditGameEntry> ArrayPool = ArrayPool<RedditGameEntry>.Create(PoolMaxGameEntry, 1);

	/// A method that gets a collection of Reddit game entries from a JSON object
	/// <summary>
	/// Gets a collection of Reddit game entries from a JSON object.
	/// </summary>
	/// <returns>A collection of Reddit game entries.</returns>
	public static async ValueTask<ICollection<RedditGameEntry>> GetGames() {
		WebBrowser? webBrowser = ArchiSteamFarm.Core.ASF.WebBrowser;
		RedditGameEntry[] result = Array.Empty<RedditGameEntry>();

		if (webBrowser is null) {
			return result;
		}

		ObjectResponse<JToken>? jsonPayload = null;

		try {
			jsonPayload = await TryGetPayload(webBrowser).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is JsonException or IOException) {
			return result;
		}

		if (jsonPayload is null) {
			return result;
		}

		// Use pattern matching to check for null and type
		if (jsonPayload.Content is JObject jObject &&
			jObject.TryGetValue("kind", out JToken? kind) &&
			(kind.Value<string>() == "Listing") &&
			jObject.TryGetValue("data", out JToken? data) &&
			data is JObject) {
			JToken? children = data["children"];

			if (children is not null) {
				return LoadMessages(children);
			}
		}

		return result; // Return early if children is not found or not an array
	}

	internal static RedditGameEntry[] LoadMessages(JToken children) {
		Span<long> bloomFilterBuffer = stackalloc long[BloomFilterBufferSize];
		StringBloomFilterSpan bloomFilter = new(bloomFilterBuffer, 3);
		RedditGameEntry[] buffer = ArrayPool.Rent(PoolMaxGameEntry / 2);

		try {
			SpanList<RedditGameEntry> list = new(buffer);

			foreach (JObject comment in children.Children<JObject>()) {
				JToken? commentData = comment.GetValue("data", StringComparison.InvariantCulture);
				string text = commentData?.Value<string>("body") ?? string.Empty;
				long date = commentData?.Value<long?>("created_utc") ?? commentData?.Value<long?>("created") ?? 0;
				MatchCollection matches = CommandRegex().Matches(text);

				foreach (Match match in matches) {
					ERedditGameEntryKind kind = ERedditGameEntryKind.None;

					if (IsPermanentlyFreeRegex().IsMatch(text)) {
						kind |= ERedditGameEntryKind.FreeToPlay;
					}

					if (IsDlcRegex().IsMatch(text)) {
						kind = ERedditGameEntryKind.Dlc;
					}

					foreach (Group matchGroup in match.Groups) {
						if (!matchGroup.Name.StartsWith("appid", StringComparison.InvariantCulture)) {
							continue;
						}

						foreach (Capture capture in matchGroup.Captures) {
							RedditGameEntry gameEntry = new(capture.Value, kind, date);

							int index = -1;

							if (bloomFilter.Contains(gameEntry.Identifier)) {
								index = list.IndexOf(gameEntry, new GameEntryIdentifierEqualityComparer());
							}

							if (index >= 0) {
								ref RedditGameEntry oldEntry = ref list[index];

								if (gameEntry.Date > oldEntry.Date) {
									oldEntry = gameEntry;
								}
							}
							else {
								list.Add(in gameEntry);
								bloomFilter.Add(gameEntry.Identifier);
							}

							while (list.Count >= list.Capacity) {
								list.RemoveAt(list.Count - 1); // Remove the last element instead of using a magic number
							}
						}
					}
				}
			}

			return list.ToArray();
		}
		finally {
			// Use a finally block to ensure that the buffer is returned to the pool
			ArrayPool.Return(buffer);
		}
	}

	[GeneratedRegex(@"(.addlicense)\s+(asf)?\s*((?<appid>(s/|a/)\d+)\s*,?\s*)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex CommandRegex();

	private static Uri GetUrl() => new($"https://www.reddit.com/user/{User}.json?sort=new", UriKind.Absolute);

	[GeneratedRegex(@"free\s+DLC\s+for\s+a", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex IsDlcRegex();

	[GeneratedRegex(@"permanently\s+free", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex IsPermanentlyFreeRegex();

	/// <summary>
	/// Tries to get a JSON object from Reddit.
	/// </summary>
	/// <param name="webBrowser">The web browser instance to use.</param>
	/// <returns>A JSON object response or null if failed.</returns>
	/// <exception cref="RedditServerException">Thrown when Reddit returns a server error.</exception>
	/// <remarks>This method is based on this GitHub issue: https://github.com/maxisoft/ASFFreeGames/issues/28</remarks>
	private static async Task<ObjectResponse<JToken>?> TryGetPayload(WebBrowser webBrowser) {
		try {
			return await webBrowser.UrlGetToJsonObject<JToken>(GetUrl(), rateLimitingDelay: 500).ConfigureAwait(false);
		}

		catch (JsonReaderException) {
			// ReSharper disable once UseAwaitUsing
			using StreamResponse? response = await webBrowser.UrlGetToStream(GetUrl(), rateLimitingDelay: 500).ConfigureAwait(false);

			if (response is not null && response.StatusCode.IsServerErrorCode()) {
				throw new RedditServerException($"Reddit server error: {response.StatusCode}", response.StatusCode);
			}

			// If no RedditServerException was thrown, re-throw the original JsonReaderException
			throw;
		}
	}
}
