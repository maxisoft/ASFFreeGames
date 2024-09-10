using System.Collections.Generic;
using ASFFreeGames.ASFExtentions.Games;
using Maxisoft.ASF.Reddit;

namespace Maxisoft.ASF.Redlib;

#pragma warning disable CA1819

public readonly record struct RedlibGameEntry(IReadOnlyCollection<GameIdentifier> GameIdentifiers, string CommentLink, EGameType TypeFlags) {
	public RedditGameEntry ToRedditGameEntry(long date = default) => new(string.Join(',', GameIdentifiers), TypeFlags.ToRedditGameEntryKind(), date);
}

#pragma warning restore CA1819
