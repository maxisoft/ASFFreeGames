﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json; // Not using System.Text.Json for JsonDocument
using System.Text.Json.Nodes; // Using System.Text.Json.Nodes for JsonNode
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Helpers.Json;
using ArchiSteamFarm.Web;
using ArchiSteamFarm.Web.Responses;
using BloomFilter;
using Maxisoft.Utils.Collections.Spans;

namespace Maxisoft.ASF.Reddit;

internal sealed partial class RedditHelper {
	private const int BloomFilterBufferSize = 8;
	private const int PoolMaxGameEntry = 1024;
	private const string User = "ASFinfo";
	private static readonly ArrayPool<RedditGameEntry> ArrayPool = ArrayPool<RedditGameEntry>.Create(PoolMaxGameEntry, 1);

	/// <summary>
	/// Gets a collection of Reddit game entries from a JSON object.
	/// </summary>
	/// <returns>A collection of Reddit game entries.</returns>
	public static async ValueTask<ICollection<RedditGameEntry>> GetGames(CancellationToken cancellationToken) {
		WebBrowser? webBrowser = ArchiSteamFarm.Core.ASF.WebBrowser;
		RedditGameEntry[] result = Array.Empty<RedditGameEntry>();

		if (webBrowser is null) {
			return result;
		}

		JsonNode? jsonPayload = await GetPayload(webBrowser, cancellationToken).ConfigureAwait(false);

		JsonNode? childrenElement = jsonPayload["data"]?["children"];

		return childrenElement is null ? result : LoadMessages(childrenElement);
	}

	internal static RedditGameEntry[] LoadMessages(JsonNode children) {
		Span<long> bloomFilterBuffer = stackalloc long[BloomFilterBufferSize];
		StringBloomFilterSpan bloomFilter = new(bloomFilterBuffer, 3);
		RedditGameEntry[] buffer = ArrayPool.Rent(PoolMaxGameEntry / 2);

		try {
			SpanList<RedditGameEntry> list = new(buffer);

			// ReSharper disable once LoopCanBePartlyConvertedToQuery
			foreach (JsonNode? comment in children.AsArray()) {
				JsonNode? commentData = comment?["data"];

				if (commentData is null) {
					continue;
				}

				long date;
				string text;

				try {
					text = commentData["body"]?.GetValue<string>() ?? string.Empty;

					try {
						date = checked((long) (commentData["created_utc"]?.GetValue<double>() ?? 0));
					}
					catch (Exception e) when (e is FormatException or InvalidOperationException) {
						date = 0;
					}

					if (!double.IsNormal(date) || (date <= 0)) {
						date = checked((long) (commentData["created"]?.GetValue<double>() ?? 0));
					}
				}
				catch (Exception e) when (e is FormatException or InvalidOperationException) {
					continue;
				}

				if (!double.IsNormal(date) || (date <= 0)) {
					continue;
				}

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
	/// <param name="cancellationToken"></param>
	/// <param name="retry"></param>
	/// <returns>A JSON object response or null if failed.</returns>
	/// <exception cref="RedditServerException">Thrown when Reddit returns a server error.</exception>
	/// <remarks>This method is based on this GitHub issue: https://github.com/maxisoft/ASFFreeGames/issues/28</remarks>
	private static async ValueTask<JsonNode> GetPayload(WebBrowser webBrowser, CancellationToken cancellationToken, uint retry = 5) {
		StreamResponse? stream = null;

		for (int t = 0; t < retry; t++) {
			try {
				stream = await webBrowser.UrlGetToStream(GetUrl(), rateLimitingDelay: 500, maxTries: 1, cancellationToken: cancellationToken).ConfigureAwait(false);

				if (stream?.Content is null) {
					throw new RedditServerException("content is null", stream?.StatusCode ?? HttpStatusCode.InternalServerError);
				}

				if (stream.StatusCode.IsServerErrorCode()) {
					throw new RedditServerException($"server error code is {stream.StatusCode}", stream.StatusCode);
				}

				JsonNode? res = await ParseJsonNode(stream, cancellationToken).ConfigureAwait(false);

				if (res is null) {
					throw new RedditServerException("empty response", stream.StatusCode);
				}

				try {
					if ((res["kind"]?.GetValue<string>() != "Listing") ||
						res["data"] is null) {
						throw new RedditServerException("invalid response", stream.StatusCode);
					}
				}
				catch (Exception e) when (e is FormatException or InvalidOperationException) {
					throw new RedditServerException("invalid response", stream.StatusCode);
				}

				return res;
			}
			catch (Exception e) when (e is JsonException or IOException or RedditServerException or HttpRequestException) {
				// If it's the last retry, re-throw the original Exception
				if (t + 1 == retry) {
					throw;
				}

				cancellationToken.ThrowIfCancellationRequested();
			}
			finally {
				if (stream is not null) {
					await stream.DisposeAsync().ConfigureAwait(false);
				}

				stream = null;
			}

			await Task.Delay((2 << (t + 1)) * 100, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
		}

		return JsonNode.Parse("{}")!;
	}

	/// <summary>
	/// Parses a JSON object from a stream response. Using not straightforward for ASF trimmed compatibility reasons
	/// </summary>
	/// <param name="stream">The stream response containing the JSON data.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The parsed JSON object, or null if parsing fails.</returns>
	private static async Task<JsonNode?> ParseJsonNode(StreamResponse stream, CancellationToken cancellationToken) {
		using StreamReader reader = new(stream.Content!, Encoding.UTF8);

		return JsonNode.Parse(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false));
	}
}
