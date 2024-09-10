using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Maxisoft.ASF.Reddit;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.FreeGames.Strategies;

[SuppressMessage("ReSharper", "RedundantNullableFlowAttribute")]
public sealed class RedditListFreeGamesStrategy : IListFreeGamesStrategy {
	public void Dispose() { }

	public async Task<IReadOnlyCollection<RedditGameEntry>> GetGames([NotNull] ListFreeGamesContext context, CancellationToken cancellationToken) {
		cancellationToken.ThrowIfCancellationRequested();

		return await RedditHelper.GetGames(context.HttpClient.Value, context.Retry, cancellationToken).ConfigureAwait(false);
	}
}
