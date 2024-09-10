using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Maxisoft.ASF.Reddit;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.FreeGames.Strategies;

[SuppressMessage("ReSharper", "RedundantNullableFlowAttribute")]
public interface IListFreeGamesStrategy : IDisposable {
	Task<IReadOnlyCollection<RedditGameEntry>> GetGames([NotNull] ListFreeGamesContext context, CancellationToken cancellationToken);

	public static Exception ExceptionFromTask<T>([NotNull] Task<T> task) {
		if (task is { IsFaulted: true, Exception: not null }) {
			return task.Exception.InnerExceptions.Count == 1 ? task.Exception.InnerExceptions[0] : task.Exception;
		}

		if (task.IsCanceled) {
			return new TaskCanceledException();
		}

		throw new InvalidOperationException("Unknown task state");
	}
}
