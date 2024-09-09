using System;

namespace Maxisoft.ASF.Redlib;

[Flags]
public enum EGameType : sbyte {
	None = 0,
	FreeToPlay = 1 << 0,
	PermenentlyFree = 1 << 1,
	Dlc = 1 << 2
}
