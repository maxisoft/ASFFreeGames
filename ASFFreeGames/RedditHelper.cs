using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArchiSteamFarm.Web.Responses;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maxisoft.ASF;

public record struct RedditGameEntry(string Identifier, bool FreeToPlay, long date);

internal sealed class RedditHelper {
	private const string User = "ASFinfo";

	[NotNull]
	private static Uri GetUrl() => new Uri($"https://www.reddit.com/user/{User}.json?sort=new", UriKind.Absolute);

	private readonly Lazy<Regex> CommandRegex = new Lazy<Regex>(
		static () => new Regex(
			@"(.addlicense)\s+(asf)?\s*((?<appid>(s/|a/)\d+)\s*,?\s*)+.*?(?<free>permanently\s+free)?",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant
		)
	);

	public async ValueTask<List<RedditGameEntry>> ListGames() {
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

		if (data is null || !data.TryGetValue("children", out var children) || children is null) {
			return res;
		}

		var regex = CommandRegex.Value;

		foreach (var comment in children.Children<JObject>()) {
			JToken? commentData = comment.GetValue("data", StringComparison.InvariantCulture);
			var text = commentData?.Value<string>("body") ?? string.Empty;
			var date = commentData?.Value<long?>("created_utc") ?? commentData?.Value<long?>("created") ?? 0;
			var match = regex.Match(text);

			if (!match.Success) {
				continue;
			}

			bool freeToPlay = match.Groups["free"].Success;

			foreach (Group matchGroup in match.Groups) {
				if (matchGroup.Name.StartsWith("appid", StringComparison.InvariantCulture)) {
					res.Add(new RedditGameEntry(matchGroup.Value, freeToPlay, date));
				}
			}
		}

		res.Sort(static (entry, otherEntry) => entry.date.CompareTo(otherEntry.date));

		return res;
	}
}
