using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using ASFFreeGames.ASFExtensions.Bot;
using Maxisoft.ASF.ASFExtensions;

namespace Maxisoft.ASF {
	/// <summary>
	/// Defines an interface for accessing and saving <see cref="BotContext"/> instances in a read-only manner.
	/// </summary>
	internal interface IRegistryReadOnly {
		/// <summary>
		/// Gets the <see cref="BotContext"/> instance associated with the specified <see cref="Bot"/>.
		/// </summary>
		/// <param name="bot">The <see cref="Bot"/> instance.</param>
		/// <returns>The <see cref="BotContext"/> instance if found; otherwise, null.</returns>
		BotContext? GetBotContext(Bot bot);
	}

	/// <summary>
	/// Defines an interface for accessing and saving <see cref="BotContext"/> instances with read/write operations.
	/// </summary>
	internal interface IContextRegistry : IRegistryReadOnly {
		/// <summary>
		/// Removes the <see cref="BotContext"/> instance associated with the specified <see cref="Bot"/>.
		/// </summary>
		/// <param name="bot">The <see cref="Bot"/> instance.</param>
		/// <returns>True if the removal was successful; otherwise, false.</returns>
		ValueTask<bool> RemoveBotContext(Bot bot);

		/// <summary>
		/// Saves the <see cref="BotContext"/> instance associated with the specified <see cref="Bot"/>.
		/// </summary>
		/// <param name="bot">The <see cref="Bot"/> instance.</param>
		/// <param name="context">The <see cref="BotContext"/> instance.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task SaveBotContext(Bot bot, BotContext context, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents a class that manages the <see cref="BotContext"/> instances for each bot using a concurrent dictionary.
	/// </summary>
	internal sealed class ContextRegistry : IContextRegistry {
		// A concurrent dictionary that maps bot names to bot contexts
		private readonly ConcurrentDictionary<BotName, BotContext> BotContexts = new();

		/// <inheritdoc/>
		public BotContext? GetBotContext(Bot bot) => BotContexts.GetValueOrDefault(bot.BotName);

		/// <inheritdoc/>
		public ValueTask<bool> RemoveBotContext(Bot bot) => ValueTask.FromResult(BotContexts.TryRemove(bot.BotName, out _));

		/// <inheritdoc/>
		public Task SaveBotContext(Bot bot, BotContext context, CancellationToken cancellationToken) => Task.FromResult(BotContexts[bot.BotName] = context);
	}
}
