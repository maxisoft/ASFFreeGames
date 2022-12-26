using System;
using System.Collections.Generic;
using System.Globalization;
using ArchiSteamFarm.Steam;

namespace Maxisoft.ASF.Configurations;

public class ASFFreeGamesOptions {
	public long RecheckIntervalMs { get; set; } = 30 * 60 * 1000;
	public int? RandomizeRecheckIntervalMs { get; set; }
	public bool? SkipFreeToPlay { get; set; }

	// ReSharper disable once InconsistentNaming
	public bool? SkipDLC { get; set; }

#pragma warning disable CA2227
	public HashSet<string> Blacklist { get; set; } = new();
#pragma warning restore CA2227

	public bool? VerboseLog { get; set; }

	#region IsBlacklisted
	public bool IsBlacklisted(in GameIdentifier gid) {
		if (Blacklist.Count <= 0) {
			return false;
		}

		return Blacklist.Contains(gid.ToString()) || Blacklist.Contains(gid.Id.ToString(NumberFormatInfo.InvariantInfo));
	}

	public bool IsBlacklisted(in Bot? bot) => bot is null || ((Blacklist.Count > 0) && Blacklist.Contains($"bot/{bot.BotName}"));
	#endregion
}
