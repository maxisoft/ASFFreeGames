using System;
using System.Collections.Generic;
using System.Threading;
using ArchiSteamFarm.Steam;

namespace Maxisoft.ASF;

internal readonly record struct PluginContext(IReadOnlyCollection<Bot> Bots, IContextRegistry BotContexts, ASFFreeGamesOptions Options, LoggerFilter LoggerFilter, Lazy<CancellationToken> CancellationTokenLazy) {
	public CancellationToken CancellationToken => CancellationTokenLazy.Value;
}
