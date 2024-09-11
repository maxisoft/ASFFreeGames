using System;
using System.Collections.Generic;
using ASFFreeGames.ASFExtentions.Games;
using Maxisoft.ASF.Reddit;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.Redlib;

#pragma warning disable CA1819

public readonly record struct RedlibGameEntry(IReadOnlyCollection<GameIdentifier> GameIdentifiers, string CommentLink, EGameType TypeFlags, DateTimeOffset Date) {
	public RedditGameEntry ToRedditGameEntry(long date = default) {
		if ((Date != default(DateTimeOffset)) && (Date != DateTimeOffset.MinValue)) {
			date = Date.ToUnixTimeMilliseconds();
		}

		return new RedditGameEntry(string.Join(',', GameIdentifiers), TypeFlags.ToRedditGameEntryKind(), date);
	}
}

#pragma warning restore CA1819
