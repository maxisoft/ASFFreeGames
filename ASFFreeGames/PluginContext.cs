using System;
using System.Collections.Generic;
using System.Threading;
using ArchiSteamFarm.Steam;
using ASFFreeGames.Configurations;
using Maxisoft.ASF.Utils;

namespace Maxisoft.ASF;

internal sealed record PluginContext(
	IReadOnlyCollection<Bot> Bots,
	IContextRegistry BotContexts,
	ASFFreeGamesOptions Options,
	LoggerFilter LoggerFilter,
	bool Valid = false
) {
	/// <summary>
	/// Gets the cancellation token associated with this context.
	/// </summary>
	public CancellationToken CancellationToken => CancellationTokenLazy.Value;

	internal Lazy<CancellationToken> CancellationTokenLazy { private get; set; } =
		new(static () => default(CancellationToken));

	/// <summary>
	/// A struct that implements IDisposable and temporarily changes the cancellation token of the PluginContext instance.
	/// </summary>
	public readonly struct CancellationTokenChanger : IDisposable {
		private readonly PluginContext Context;
		private readonly Lazy<CancellationToken> Original;

		/// <summary>
		/// Initializes a new instance of the <see cref="CancellationTokenChanger"/> struct with the specified context and factory.
		/// </summary>
		/// <param name="context">The PluginContext instance to change.</param>
		/// <param name="factory">The function that creates a new cancellation token.</param>
		public CancellationTokenChanger(PluginContext context, Func<CancellationToken> factory) {
			Context = context;
			Original = context.CancellationTokenLazy;
			context.CancellationTokenLazy = new Lazy<CancellationToken>(factory);
		}

		/// <inheritdoc />
		/// <summary>
		/// Restores the original cancellation token to the PluginContext instance.
		/// </summary>
		public void Dispose() => Context.CancellationTokenLazy = Original;
	}

	/// <summary>
	/// Creates a new instance of the <see cref="CancellationTokenChanger"/> struct with the specified factory.
	/// </summary>
	/// <param name="factory">The function that creates a new cancellation token.</param>
	/// <returns>A new instance of the <see cref="CancellationTokenChanger"/> struct.</returns>
	public CancellationTokenChanger TemporaryChangeCancellationToken(
		Func<CancellationToken> factory
	) => new(this, factory);
}
