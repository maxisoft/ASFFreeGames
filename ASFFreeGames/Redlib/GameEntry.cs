using System.Collections.Generic;
using ASFFreeGames.ASFExtentions.Games;

namespace Maxisoft.ASF.Redlib;

#pragma warning disable CA1819

public readonly record struct GameEntry(IReadOnlyCollection<GameIdentifier> GameIdentifiers, string CommentLink, EGameType TypeFlags) { }

#pragma warning restore CA1819
