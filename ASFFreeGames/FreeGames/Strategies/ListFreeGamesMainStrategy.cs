using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maxisoft.ASF.HttpClientSimple;
using Maxisoft.ASF.Reddit;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.FreeGames.Strategies;

[SuppressMessage("ReSharper", "RedundantNullableFlowAttribute")]
public class ListFreeGamesMainStrategy : IListFreeGamesStrategy {
	private readonly RedditListFreeGamesStrategy RedditStrategy = new();
	private readonly RedlibListFreeGamesStrategy RedlibStrategy = new();

	private SemaphoreSlim StrategySemaphore { get; } = new(1, 1); // prevents concurrent run and access to internal state

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public async Task<IReadOnlyCollection<RedditGameEntry>> GetGames(
		[NotNull] ListFreeGamesContext context,
		CancellationToken cancellationToken
	) {
		await StrategySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try {
			return await DoGetGames(context, cancellationToken).ConfigureAwait(false);
		}
		finally {
			StrategySemaphore.Release();
		}
	}

	protected virtual void Dispose(bool disposing) {
		if (disposing) {
			RedditStrategy.Dispose();
			RedlibStrategy.Dispose();
			StrategySemaphore.Dispose();
		}
	}

	private async Task<IReadOnlyCollection<RedditGameEntry>> DoGetGames(
		[NotNull] ListFreeGamesContext context,
		CancellationToken cancellationToken
	) {
		using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken
		);
		List<IDisposable> disposables = [];

		try {
			Task<IReadOnlyCollection<RedditGameEntry>> redditTask1 = FirstTryRedditStrategy(
				context,
				disposables,
				cts.Token
			);
			disposables.Add(redditTask1);

			try {
				await WaitForFirstTryRedditStrategy(context, redditTask1, cts.Token)
					.ConfigureAwait(false);
			}
			catch (Exception) {
				// ignored and handled below
			}

			if (redditTask1.IsCompletedSuccessfully) {
				IReadOnlyCollection<RedditGameEntry> result = await redditTask1.ConfigureAwait(
					false
				);

				if (result.Count > 0) {
					context.PreviousSucessfulStrategy |= EListFreeGamesStrategy.Reddit;

					return result;
				}
			}

			CancellationTokenSource cts2 = CancellationTokenSource.CreateLinkedTokenSource(
				cts.Token
			);
			disposables.Add(cts2);
			cts2.CancelAfter(TimeSpan.FromSeconds(45));

			Task<IReadOnlyCollection<RedditGameEntry>> redlibTask = RedlibStrategy.GetGames(
				context with {
					HttpClient = new Lazy<SimpleHttpClient>(
						() => context.HttpClientFactory.CreateForRedlib()
					),
				},
				cts2.Token
			);
			disposables.Add(redlibTask);

			Task<IReadOnlyCollection<RedditGameEntry>> redditTask2 = LastTryRedditStrategy(
				context,
				redditTask1,
				cts2.Token
			);
			disposables.Add(redditTask2);

			context.PreviousSucessfulStrategy = EListFreeGamesStrategy.None;

			Task<IReadOnlyCollection<RedditGameEntry>>[] strategiesTasks =
			[
				redditTask1,
				redditTask2,
				redlibTask,
			]; // note that order matters

			try {
				IReadOnlyCollection<RedditGameEntry>? res = await WaitForStrategiesTasks(
						cts.Token,
						strategiesTasks
					)
					.ConfigureAwait(false);

				if (res is { Count: > 0 }) {
					return res;
				}
			}
			finally {
				if (redditTask1.IsCompletedSuccessfully || redditTask2.IsCompletedSuccessfully) {
					context.PreviousSucessfulStrategy |= EListFreeGamesStrategy.Reddit;
				}

#pragma warning disable CA1849
				if (redlibTask is { IsCompletedSuccessfully: true, Result.Count: > 0 }) {
#pragma warning restore CA1849
					context.PreviousSucessfulStrategy |= EListFreeGamesStrategy.Redlib;
				}

				await cts.CancelAsync().ConfigureAwait(false);
				await cts2.CancelAsync().ConfigureAwait(false);

				try {
					await Task.WhenAll(strategiesTasks).ConfigureAwait(false);
				}
				catch (Exception) {
					// ignored
				}
			}

			List<Exception> exceptions = new(strategiesTasks.Length);
			exceptions.AddRange(
				from task in strategiesTasks
				where task.IsFaulted || task.IsCanceled
				select IListFreeGamesStrategy.ExceptionFromTask(task)
			);

			switch (exceptions.Count) {
				case 1:
					throw exceptions[0];
				case > 0:
					throw new AggregateException(exceptions);
			}
		}
		finally {
			foreach (IDisposable disposable in disposables) {
				disposable.Dispose();
			}
		}

		cancellationToken.ThrowIfCancellationRequested();

		throw new InvalidOperationException("This should never happen");
	}

	// ReSharper disable once SuggestBaseTypeForParameter
	private async Task<IReadOnlyCollection<RedditGameEntry>> FirstTryRedditStrategy(
		ListFreeGamesContext context,
		List<IDisposable> disposables,
		CancellationToken cancellationToken
	) {
		CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken
		);
		disposables.Add(cts);
		cts.CancelAfter(TimeSpan.FromSeconds(10));

		if (!context.PreviousSucessfulStrategy.HasFlag(EListFreeGamesStrategy.Reddit)) {
			await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
		}

		return await RedditStrategy
			.GetGames(
				context with {
					Retry = 1,
					HttpClient = new Lazy<SimpleHttpClient>(
						() => context.HttpClientFactory.CreateForReddit()
					),
				},
				cts.Token
			)
			.ConfigureAwait(false);
	}

	private async Task<IReadOnlyCollection<RedditGameEntry>> LastTryRedditStrategy(
		ListFreeGamesContext context,
		Task firstTryTask,
		CancellationToken cancellationToken
	) {
		if (!firstTryTask.IsCompleted) {
			try {
				await firstTryTask.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception) {
				// ignored it'll be handled by caller
			}
		}

		cancellationToken.ThrowIfCancellationRequested();

		return await RedditStrategy
			.GetGames(
				context with {
					Retry = checked(context.Retry - 1),
					HttpClient = new Lazy<SimpleHttpClient>(
						() => context.HttpClientFactory.CreateForReddit()
					),
				},
				cancellationToken
			)
			.ConfigureAwait(false);
	}

	private static async Task WaitForFirstTryRedditStrategy(
		ListFreeGamesContext context,
		Task redditTask,
		CancellationToken cancellationToken
	) {
		if (context.PreviousSucessfulStrategy.HasFlag(EListFreeGamesStrategy.Reddit)) {
			try {
				await Task.WhenAny(redditTask, Task.Delay(2500, cancellationToken))
					.ConfigureAwait(false);
			}
			catch (Exception e) {
				if (
					e is OperationCanceledException or TimeoutException
					&& cancellationToken.IsCancellationRequested
				) {
					throw;
				}
			}
		}
	}

	private static async Task<IReadOnlyCollection<RedditGameEntry>?> WaitForStrategiesTasks(
		CancellationToken cancellationToken,
		params Task<IReadOnlyCollection<RedditGameEntry>>[] p
	) {
		LinkedList<Task<IReadOnlyCollection<RedditGameEntry>>> tasks = [];

		foreach (Task<IReadOnlyCollection<RedditGameEntry>> task in p) {
			tasks.AddLast(task);
		}

		while ((tasks.Count != 0) && !cancellationToken.IsCancellationRequested) {
			try {
				await Task.WhenAny(tasks).ConfigureAwait(false);
			}
			catch (Exception) {
				// ignored
			}

			LinkedListNode<Task<IReadOnlyCollection<RedditGameEntry>>>? taskNode = tasks.First;

			while (taskNode is not null) {
				if (taskNode.Value.IsCompletedSuccessfully) {
					IReadOnlyCollection<RedditGameEntry> result =
						await taskNode.Value.ConfigureAwait(false);

					if (result.Count > 0) {
						return result;
					}
				}

				if (taskNode.Value.IsCompleted) {
					tasks.Remove(taskNode.Value);
					taskNode = tasks.First;

					continue;
				}

				taskNode = taskNode.Next;
			}
		}

		return null;
	}
}
