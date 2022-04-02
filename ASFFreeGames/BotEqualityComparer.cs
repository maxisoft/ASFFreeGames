using System;
using System.Collections.Generic;
using ArchiSteamFarm.Steam;

namespace Maxisoft.ASF;

internal sealed class BotEqualityComparer : IEqualityComparer<Bot> {
	public bool Equals(Bot? x, Bot? y) {
		if (ReferenceEquals(x, y)) {
			return true;
		}

		if (ReferenceEquals(x, null)) {
			return false;
		}

		if (ReferenceEquals(y, null)) {
			return false;
		}

		return (x.BotName == y.BotName) && (x.SteamID == y.SteamID);
	}

	public int GetHashCode(Bot? obj) => obj != null ? HashCode.Combine(obj.BotName, obj.SteamID) : 0;
}
