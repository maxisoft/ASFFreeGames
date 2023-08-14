using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using Maxisoft.ASF;

namespace ASFFreeGames.Commands {
	// Implement the IBotCommand interface
	internal sealed class CommandDispatcher : IBotCommand {
		// Declare a private field for the plugin options instance
		private readonly ASFFreeGamesOptions Options;

		// Declare a private field for the dictionary that maps command names to IBotCommand instances
		private readonly Dictionary<string, IBotCommand> Commands;

		// Define a constructor that takes an plugin options instance as a parameter
		public CommandDispatcher(ASFFreeGamesOptions options) {
			Options = options ?? throw new ArgumentNullException(nameof(options));

			// Initialize the commands dictionary with instances of GetIPCommand and FreeGamesCommand
			Commands = new Dictionary<string, IBotCommand>(StringComparer.OrdinalIgnoreCase) {
				{ "GETIP", new GetIPCommand() },
				{ "FREEGAMES", new FreeGamesCommand(options) }
			};
		}

		// Define a method named Execute that takes the bot, message, args, steamID, and cancellationToken parameters and returns a string response
		public async Task<string?> Execute(Bot? bot, string message, string[] args, ulong steamID = 0, CancellationToken cancellationToken = default) {
			if (args is { Length: > 0 }) {
				// Try to get the corresponding IBotCommand instance from the commands dictionary based on the first argument
				if (Commands.TryGetValue(args[0], out IBotCommand? command)) {
					// Delegate the command execution to the IBotCommand instance, passing the bot and other parameters
					return await command.Execute(bot, message, args, steamID, cancellationToken).ConfigureAwait(false);
				}
			}

			return null;
		}
	}
}
