using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Web;
using ArchiSteamFarm.Web.Responses;
using Maxisoft.ASF;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ASFFreeGames.Commands;

internal sealed class GetIPCommand : IBotCommand {
	private const string GetIPAddressUrl = "https://httpbin.org/ip";

	public async Task<string?> Execute(Bot? bot, string message, string[] args, ulong steamID = 0, CancellationToken cancellationToken = default) {
		WebBrowser? web = IBotCommand.GetWebBrowser(bot);

		if (web is null) {
			return IBotCommand.FormatBotResponse(bot, "unable to get a valid web browser");
		}

		if (cancellationToken.IsCancellationRequested) {
			return "";
		}

		try {
			ObjectResponse<JToken>? result = await web.UrlGetToJsonObject<JToken>(new Uri(GetIPAddressUrl)).ConfigureAwait(false);
			string origin = result?.Content?.Value<string>("origin") ?? "";

			if (!string.IsNullOrWhiteSpace(origin)) {
				return IBotCommand.FormatBotResponse(bot, origin);
			}
		}
		catch (Exception e) when (e is JsonException or IOException) {
			return IBotCommand.FormatBotResponse(bot, string.Format(CultureInfo.CurrentCulture, Strings.ErrorIsInvalid, e.Message));
		}

		return null;
	}
}
