using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Web;
using ArchiSteamFarm.Web.Responses;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ASFFreeGames.Commands.GetIp;

// ReSharper disable once ClassNeverInstantiated.Local
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
			await using StreamResponse? result = await web.UrlGetToStream(new Uri(GetIPAddressUrl), cancellationToken: cancellationToken).ConfigureAwait(false);

			if (result?.Content is null) { return null; }

			GetIpReponse? reponse = await JsonSerializer.DeserializeAsync<GetIpReponse>(result.Content, cancellationToken: cancellationToken).ConfigureAwait(false);
			string? origin = reponse?.Origin;

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
