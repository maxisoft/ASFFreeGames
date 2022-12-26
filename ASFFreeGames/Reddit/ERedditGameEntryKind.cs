using System;

namespace Maxisoft.ASF.Reddit;

[Flags]
public enum ERedditGameEntryKind : byte {
	None = 0,
	FreeToPlay = 1,
	Dlc = 1 << 1
}
