using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using ArchiSteamFarm.Steam;
using Maxisoft.ASF;

namespace ASFFreeGames.Configurations;

public class ASFFreeGamesOptions {
	// Use TimeSpan instead of long for representing time intervals
	[JsonPropertyName("recheckInterval")]
	public TimeSpan RecheckInterval { get; set; } = TimeSpan.FromMinutes(30);

	// Use Nullable<T> instead of bool? for nullable value types
	[JsonPropertyName("randomizeRecheckInterval")]
	public bool? RandomizeRecheckInterval { get; set; }

	[JsonPropertyName("skipFreeToPlay")]
	public bool? SkipFreeToPlay { get; set; }

	// ReSharper disable once InconsistentNaming
	[JsonPropertyName("skipDLC")]
	public bool? SkipDLC { get; set; }

	// Use IReadOnlyCollection<string> instead of HashSet<string> for blacklist property
	[JsonPropertyName("blacklist")]
	public IReadOnlyCollection<string> Blacklist { get; set; } = new HashSet<string>();

	[JsonPropertyName("verboseLog")]
	public bool? VerboseLog { get; set; }

	#region IsBlacklisted
	public bool IsBlacklisted(in GameIdentifier gid) {
		if (Blacklist.Count <= 0) {
			return false;
		}

		return Blacklist.Contains(gid.ToString()) || Blacklist.Contains(gid.Id.ToString(CultureInfo.InvariantCulture));
	}

	public bool IsBlacklisted(in Bot? bot) => bot is null || ((Blacklist.Count > 0) && Blacklist.Contains($"bot/{bot.BotName}"));
	#endregion
}


