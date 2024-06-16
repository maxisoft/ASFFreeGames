using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using Maxisoft.ASF.HttpClientSimple;
using Maxisoft.Utils.Collections.Dictionaries;

namespace Maxisoft.ASF.Reddit;

internal sealed class RedditHelper {
	private const int MaxGameEntry = 1024;
	private const string User = "ASFinfo";

	/// <summary>
	///     Gets a collection of Reddit game entries from a JSON object.
	/// </summary>
	/// <returns>A collection of Reddit game entries.</returns>
	public static async ValueTask<IReadOnlyCollection<RedditGameEntry>> GetGames(SimpleHttpClient httpClient, CancellationToken cancellationToken) {
		// ReSharper disable once UseCollectionExpression
		RedditGameEntry[] result = Array.Empty<RedditGameEntry>();

		JsonNode? jsonPayload = await GetPayload(httpClient, cancellationToken).ConfigureAwait(false);

		JsonNode? childrenElement = jsonPayload["data"]?["children"];

		return childrenElement is null ? result : LoadMessages(childrenElement);
	}

	internal static IReadOnlyCollection<RedditGameEntry> LoadMessages(JsonNode children) {
		OrderedDictionary<RedditGameEntry, EmptyStruct> games = new(new GameEntryIdentifierEqualityComparer());

		IReadOnlyCollection<RedditGameEntry> returnValue() {
			while (games.Count is > 0 and > MaxGameEntry) {
				games.RemoveAt((^1).GetOffset(games.Count));
			}

			return (IReadOnlyCollection<RedditGameEntry>) games.Keys;
		}

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

			MatchCollection matches = RedditHelperRegexes.Command().Matches(text);

			foreach (Match match in matches) {
				ERedditGameEntryKind kind = ERedditGameEntryKind.None;

				if (RedditHelperRegexes.IsPermanentlyFree().IsMatch(text)) {
					kind |= ERedditGameEntryKind.FreeToPlay;
				}

				if (RedditHelperRegexes.IsDlc().IsMatch(text)) {
					kind = ERedditGameEntryKind.Dlc;
				}

				foreach (Group matchGroup in match.Groups) {
					if (!matchGroup.Name.StartsWith("appid", StringComparison.InvariantCulture)) {
						continue;
					}

					foreach (Capture capture in matchGroup.Captures) {
						RedditGameEntry gameEntry = new(capture.Value, kind, date);

						try {
							games.Add(gameEntry, default(EmptyStruct));
						}
						catch (ArgumentException) { }

						if (games.Count >= MaxGameEntry) {
							return returnValue();
						}
					}
				}
			}
		}

		return returnValue();
	}

	/// <summary>
	///     Tries to get a JSON object from Reddit.
	/// </summary>
	/// <param name="httpClient">The http client instance to use.</param>
	/// <param name="cancellationToken"></param>
	/// <param name="retry"></param>
	/// <returns>A JSON object response or null if failed.</returns>
	/// <exception cref="RedditServerException">Thrown when Reddit returns a server error.</exception>
	/// <remarks>This method is based on this GitHub issue: https://github.com/maxisoft/ASFFreeGames/issues/28</remarks>
	private static async ValueTask<JsonNode> GetPayload(SimpleHttpClient httpClient, CancellationToken cancellationToken, uint retry = 5) {
		HttpStreamResponse? response = null;

		Dictionary<string, string> headers = new() {
			{ "Pragma", "no-cache" },
			{ "Cache-Control", "no-cache" },
			{ "Accept", "application/json" },
			{ "Sec-Fetch-Site", "none" },
			{ "Sec-Fetch-Mode", "no-cors" },
			{ "Sec-Fetch-Dest", "empty" },
			{ "x-sec-fetch-dest", "empty" },
			{ "x-sec-fetch-mode", "no-cors" },
			{ "x-sec-fetch-site", "none" },
		};

		for (int t = 0; t < retry; t++) {
			try {
#pragma warning disable CA2000
				response = await httpClient.GetStreamAsync(GetUrl(), headers, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2000

				if (await HandleTooManyRequest(response, cancellationToken: cancellationToken).ConfigureAwait(false)) {
					continue;
				}

				if (!response.StatusCode.IsSuccessCode()) {
					throw new RedditServerException($"reddit http error code is {response.StatusCode}", response.StatusCode);
				}

				JsonNode? res = await ParseJsonNode(response, cancellationToken).ConfigureAwait(false);

				if (res is null) {
					throw new RedditServerException("empty response", response.StatusCode);
				}

				try {
					if ((res["kind"]?.GetValue<string>() != "Listing") ||
						res["data"] is null) {
						throw new RedditServerException("invalid response", response.StatusCode);
					}
				}
				catch (Exception e) when (e is FormatException or InvalidOperationException) {
					throw new RedditServerException("invalid response", response.StatusCode);
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
				if (response is not null) {
					await response.DisposeAsync().ConfigureAwait(false);
				}

				response = null;
			}

			await Task.Delay((2 << (t + 1)) * 100, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
		}

		return JsonNode.Parse("{}")!;
	}

	/// <summary>
	/// Handles too many requests by checking the status code and headers of the response.
	/// If the status code is Forbidden or TooManyRequests, it checks the remaining rate limit
	/// and the reset time. If the remaining rate limit is less than or equal to 0, it delays
	/// the execution until the reset time using the cancellation token.
	/// </summary>
	/// <param name="response">The HTTP stream response to handle.</param>
	/// <param name="maxTimeToWait"></param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the request was handled & awaited, false otherwise.</returns>
	private static async ValueTask<bool> HandleTooManyRequest(HttpStreamResponse response, int maxTimeToWait = 45, CancellationToken cancellationToken = default) {
		if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) {
			if (response.Response.Headers.TryGetValues("x-ratelimit-remaining", out IEnumerable<string>? rateLimitRemaining)) {
				if (int.TryParse(rateLimitRemaining.FirstOrDefault(), out int remaining) && (remaining <= 0)) {
					if (response.Response.Headers.TryGetValues("x-ratelimit-reset", out IEnumerable<string>? rateLimitReset)
						&& float.TryParse(rateLimitReset.FirstOrDefault(), out float reset) && double.IsNormal(reset) && (0 < reset) && (reset < maxTimeToWait)) {
						try {
							await Task.Delay(TimeSpan.FromSeconds(reset), cancellationToken).ConfigureAwait(false);
						}
						catch (TaskCanceledException) {
							return false;
						}
						catch (TimeoutException) {
							return false;
						}
						catch (OperationCanceledException) {
							return false;
						}
					}

					return true;
				}
			}
		}

		return false;
	}

	private static Uri GetUrl() => new($"https://www.reddit.com/user/{User}.json?sort=new", UriKind.Absolute);

	/// <summary>
	///     Parses a JSON object from a stream response. Using not straightforward for ASF trimmed compatibility reasons
	/// </summary>
	/// <param name="stream">The stream response containing the JSON data.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The parsed JSON object, or null if parsing fails.</returns>
	private static async Task<JsonNode?> ParseJsonNode(HttpStreamResponse stream, CancellationToken cancellationToken) {
		string data = await stream.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		return JsonNode.Parse(data);
	}
}
