using System;
using System.Collections.Generic;

namespace Maxisoft.ASF.Reddit;

internal readonly struct GameEntryIdentifierEqualityComparer : IEqualityComparer<RedditGameEntry> {
	public bool Equals(RedditGameEntry x, RedditGameEntry y) => string.Equals(x.Identifier, y.Identifier, StringComparison.OrdinalIgnoreCase);

	public int GetHashCode(RedditGameEntry obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Identifier);
}
