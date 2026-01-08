using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Web;

namespace ASFFreeGames.Commands;

// Define an interface named IBotCommand
internal interface IBotCommand {
	// Define a method named Execute that takes the bot, message, args, steamID, and cancellationToken parameters and returns a string response
	Task<string?> Execute(
		Bot? bot,
		string message,
		string[] args,
		ulong steamID = 0,
		CancellationToken cancellationToken = default
	);

	protected static string FormatBotResponse(Bot? bot, string resp) =>
		bot?.Commands?.FormatBotResponse(resp)
		?? ArchiSteamFarm.Steam.Interaction.Commands.FormatStaticResponse(resp);
	protected static WebBrowser? GetWebBrowser(Bot? bot) =>
		bot?.ArchiWebHandler?.WebBrowser ?? ArchiSteamFarm.Core.ASF.WebBrowser;
}
