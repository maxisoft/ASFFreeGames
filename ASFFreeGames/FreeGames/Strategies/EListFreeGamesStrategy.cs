using System;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.FreeGames.Strategies;

[Flags]
public enum EListFreeGamesStrategy {
	None = 0,
	Reddit = 1 << 0,
	Redlib = 1 << 1
}
