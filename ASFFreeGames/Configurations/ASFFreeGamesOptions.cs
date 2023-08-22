using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArchiSteamFarm.Steam;
using Maxisoft.ASF;
using Newtonsoft.Json;

namespace Maxisoft.ASF {
	public class ASFFreeGamesOptions {
		// Use TimeSpan instead of long for representing time intervals
		[JsonProperty("recheckInterval")]
		public TimeSpan RecheckInterval { get; set; } = TimeSpan.FromMinutes(30);

		// Use Nullable<T> instead of bool? for nullable value types
		[JsonProperty("randomizeRecheckInterval")]
		public Nullable<bool> RandomizeRecheckInterval { get; set; }

		[JsonProperty("skipFreeToPlay")]
		public Nullable<bool> SkipFreeToPlay { get; set; }

		// ReSharper disable once InconsistentNaming
		[JsonProperty("skipDLC")]
		public Nullable<bool> SkipDLC { get; set; }

		// Use IReadOnlyCollection<string> instead of HashSet<string> for blacklist property
		[JsonProperty("blacklist")]
		public IReadOnlyCollection<string> Blacklist { get; set; } = new HashSet<string>();

		[JsonProperty("verboseLog")]
		public Nullable<bool> VerboseLog { get; set; }

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
}
