using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using ASFFreeGames.Commands.GetIp;
using ASFFreeGames.Configurations;

namespace ASFFreeGames.Commands {
	// Implement the IBotCommand interface
	internal sealed class CommandDispatcher(ASFFreeGamesOptions options) : IBotCommand {
		// Declare a private field for the plugin options instance
		private readonly ASFFreeGamesOptions Options = options ?? throw new ArgumentNullException(nameof(options));

		// Declare a private field for the dictionary that maps command names to IBotCommand instances
		private readonly Dictionary<string, IBotCommand> Commands = new(StringComparer.OrdinalIgnoreCase) {
			{ "GETIP", new GetIPCommand() },
			{ "FREEGAMES", new FreeGamesCommand(options) }
		};

		// Define a constructor that takes an plugin options instance as a parameter
		// Initialize the commands dictionary with instances of GetIPCommand and FreeGamesCommand

		public async Task<string?> Execute(Bot? bot, string message, string[] args, ulong steamID = 0, CancellationToken cancellationToken = default) {
			try {
				if (args is { Length: > 0 }) {
					// Try to get the corresponding IBotCommand instance from the commands dictionary based on the first argument
					if (Commands.TryGetValue(args[0], out IBotCommand? command)) {
						// Delegate the command execution to the IBotCommand instance, passing the bot and other parameters
						return await command.Execute(bot, message, args, steamID, cancellationToken).ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex) {
				// Check if verbose logging is enabled or if the build is in debug mode
				// ReSharper disable once RedundantAssignment
				bool verboseLogging = Options.VerboseLog ?? false;
#if DEBUG
				verboseLogging = true; // Enable verbose logging in debug mode
#endif

				if (verboseLogging) {
					// Log the detailed stack trace and full description of the exception
					ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericException(ex);
				}
				else {
					// Log a compact error message
					ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericError($"An error occurred: {ex.GetType().Name} {ex.Message}");
				}
			}

			return null; // Return null if an exception occurs or if no command is found
		}
	}
}
