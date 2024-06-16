using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Web;
using ArchiSteamFarm.Web.Responses;
using Maxisoft.Utils.Collections.Dictionaries;

namespace Maxisoft.ASF.Reddit;

internal sealed class RedditHelper {
	private const int MaxGameEntry = 1024;
	private const string User = "ASFinfo";

	/// <summary>
	///     Gets a collection of Reddit game entries from a JSON object.
	/// </summary>
	/// <returns>A collection of Reddit game entries.</returns>
	public static async ValueTask<IReadOnlyCollection<RedditGameEntry>> GetGames(CancellationToken cancellationToken) {
		WebBrowser? webBrowser = ArchiSteamFarm.Core.ASF.WebBrowser;

		// ReSharper disable once UseCollectionExpression
		RedditGameEntry[] result = Array.Empty<RedditGameEntry>();

		if (webBrowser is null) {
			return result;
		}

		JsonNode? jsonPayload = await GetPayload(webBrowser, cancellationToken).ConfigureAwait(false);

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
				stream = await webBrowser.UrlGetToStream(GetUrl(), maxTries: 1, cancellationToken: cancellationToken).ConfigureAwait(false);

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

	private static Uri GetUrl() => new($"https://www.reddit.com/user/{User}.json?sort=new", UriKind.Absolute);

	/// <summary>
	///     Parses a JSON object from a stream response. Using not straightforward for ASF trimmed compatibility reasons
	/// </summary>
	/// <param name="stream">The stream response containing the JSON data.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The parsed JSON object, or null if parsing fails.</returns>
	private static async Task<JsonNode?> ParseJsonNode(StreamResponse stream, CancellationToken cancellationToken) {
		using StreamReader reader = new(stream.Content!, Encoding.UTF8);

		return JsonNode.Parse(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false));
	}
}
