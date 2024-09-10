using System;
using Maxisoft.ASF.Reddit;

namespace Maxisoft.ASF.Redlib;

[Flags]
public enum EGameType : sbyte {
	None = 0,
	FreeToPlay = 1 << 0,
	PermenentlyFree = 1 << 1,
	Dlc = 1 << 2
}

public static class GameTypeExtensions {
	public static ERedditGameEntryKind ToRedditGameEntryKind(this EGameType type) {
		ERedditGameEntryKind res = ERedditGameEntryKind.None;

		if (type.HasFlag(EGameType.FreeToPlay)) {
			res |= ERedditGameEntryKind.FreeToPlay;
		}

		if (type.HasFlag(EGameType.Dlc)) {
			res |= ERedditGameEntryKind.Dlc;
		}

		return res;
	}
}
