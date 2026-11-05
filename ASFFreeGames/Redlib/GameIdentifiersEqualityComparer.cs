using System;
using System.Collections.Generic;
using ASFFreeGames.ASFExtensions.Games;

namespace Maxisoft.ASF.Redlib;

#pragma warning disable CA1819

public sealed class GameIdentifiersEqualityComparer : IEqualityComparer<RedlibGameEntry> {
	public bool Equals(RedlibGameEntry x, RedlibGameEntry y) {
		if (x.GameIdentifiers.Count != y.GameIdentifiers.Count) {
			return false;
		}

		using IEnumerator<GameIdentifier> xIt = x.GameIdentifiers.GetEnumerator();
		using IEnumerator<GameIdentifier> yIt = y.GameIdentifiers.GetEnumerator();

		while (xIt.MoveNext() && yIt.MoveNext()) {
			if (!xIt.Current.Equals(yIt.Current)) {
				return false;
			}
		}

		return true;
	}

	public int GetHashCode(RedlibGameEntry obj) {
		HashCode h = new();

		foreach (GameIdentifier id in obj.GameIdentifiers) {
			h.Add(id);
		}

		return h.ToHashCode();
	}
}

#pragma warning restore CA1819
