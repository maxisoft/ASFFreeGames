using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using ArchiSteamFarm.Steam;

namespace Maxisoft.ASF.Utils.Workarounds;

/// <summary>
///     Provides resilient package ownership checks for bots with automatic fallback strategies.
///     Implements caching and hot-reload awareness for improved performance and reliability.
/// </summary>
public static class BotPackageChecker {
	/// <summary>
	///     Checks if a bot owns a specific package using multiple strategies:
	///     1. Direct property access (fast path)
	///     2. Cached reflection metadata
	///     3. Full reflection fallback
	/// </summary>
	/// <param name="bot">Target bot instance</param>
	/// <param name="appId">Steam application ID to check</param>
	/// <returns>
	///     True if the bot owns the package, false otherwise.
	///     Returns false for null bots or invalid app IDs.
	/// </returns>
	public static bool BotOwnsPackage(Bot? bot, uint appId) {
		if (bot is null) {
			return false;
		}

		try {
			MaintainHotReloadAwareness();

			if (TryGetCachedResult(bot, appId, out bool cachedResult)) {
				return cachedResult;
			}

			bool result = CheckOwnership(bot, appId);
			UpdateCache(bot, appId, result);

			return result;
		}
		catch (Exception e) {
			bot.ArchiLogger.LogGenericException(e);

			return false;
		}
	}

	#region Cache Configuration
	private static readonly ConcurrentDictionary<string, ConcurrentDictionary<uint, bool>> OwnershipCache = new();
	private static readonly Lock CacheLock = new();
	private static Guid LastKnownBotAssemblyMvid;
	#endregion

	#region Reflection State
	private static bool? DirectAccessValid;
	private static PropertyInfo? CachedOwnershipProperty;
	#endregion

	#region Core Implementation
	private static bool CheckOwnership(Bot bot, uint appId) {
		// Attempt direct access first when possible
		if (DirectAccessValid is not false) {
			DirectAccessValid = false; // the MissingMemberException may not be caught in this very method. this act as a guard if that fails

			try {
				bool result = DirectOwnershipCheck(bot, appId);
				DirectAccessValid = true;

				return result;
			}
			catch (Exception e) {
				DirectAccessValid = false;
				bot.ArchiLogger.LogGenericError($"Direct access failed: {e.Message}");
			}
		}

		return ReflectiveOwnershipCheck(bot, appId);
	}

	// ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
	private static bool DirectOwnershipCheck(Bot bot, uint appId) => bot.OwnedPackages?.ContainsKey(appId) ?? false;
	#endregion

	#region Reflection Implementation
	private static bool ReflectiveOwnershipCheck(Bot bot, uint appId) {
		PropertyInfo? property = GetOwnershipProperty(bot);
		object? ownedPackages = property?.GetValue(bot);

		if (ownedPackages is null) {
			bot.ArchiLogger.LogGenericError("Owned packages property is null");

			return false;
		}

		Type dictType = ownedPackages.GetType();

		Type? iDictType = dictType.GetInterface("System.Collections.Generic.IDictionary`2") ??
			dictType.GetInterface("System.Collections.Generic.IReadOnlyDictionary`2");

		if (iDictType is null) {
			bot.ArchiLogger.LogGenericError("Owned packages is not a recognized dictionary type");

			return false;
		}

		Type keyType = iDictType.GetGenericArguments()[0];
		object convertedKey;

		try {
			convertedKey = Convert.ChangeType(appId, keyType, CultureInfo.InvariantCulture);
		}
		catch (OverflowException) {
			bot.ArchiLogger.LogGenericError($"Overflow converting AppID {appId} to {keyType.Name}");

			return false;
		}
		catch (InvalidCastException) {
			bot.ArchiLogger.LogGenericError($"Invalid cast converting AppID {appId} to {keyType.Name}");

			return false;
		}

		MethodInfo? containsKeyMethod = iDictType.GetMethod("ContainsKey");

		if (containsKeyMethod is null) {
			bot.ArchiLogger.LogGenericError("ContainsKey method not found on dictionary");

			return false;
		}

		try {
			return (bool) (containsKeyMethod.Invoke(ownedPackages, [convertedKey]) ?? false);
		}
		catch (TargetInvocationException e) {
			bot.ArchiLogger.LogGenericError($"Invocation of {containsKeyMethod.Name} failed: {e.InnerException?.Message ?? e.Message}");

			return false;
		}
	}

	private static PropertyInfo? GetOwnershipProperty(Bot bot) {
		if (CachedOwnershipProperty != null) {
			return CachedOwnershipProperty;
		}

		const StringComparison comparison = StringComparison.Ordinal;
		PropertyInfo[] properties = typeof(Bot).GetProperties(BindingFlags.Public | BindingFlags.Instance);

		// ReSharper disable once LoopCanBePartlyConvertedToQuery
		foreach (PropertyInfo property in properties) {
			if (property.Name.Equals("OwnedPackages", comparison) ||
				property.Name.Equals("OwnedPackageIDs", comparison)) {
				CachedOwnershipProperty = property;

				return property;
			}
		}

		bot.ArchiLogger.LogGenericError("Valid ownership property not found");

		return null;
	}
	#endregion

	#region Cache Management
	private static void MaintainHotReloadAwareness() {
		Guid currentMvid = typeof(Bot).Assembly.ManifestModule.ModuleVersionId;

		lock (CacheLock) {
			if (currentMvid != LastKnownBotAssemblyMvid) {
				OwnershipCache.Clear();
				CachedOwnershipProperty = null;
				DirectAccessValid = null;
				LastKnownBotAssemblyMvid = currentMvid;
			}
		}
	}

	private static bool TryGetCachedResult(Bot bot, uint appId, out bool result) {
		ConcurrentDictionary<uint, bool> botCache = OwnershipCache.GetOrAdd(
			bot.BotName,
			static _ => new ConcurrentDictionary<uint, bool>()
		);

		return botCache.TryGetValue(appId, out result);
	}

	private static void UpdateCache(Bot bot, uint appId, bool result) {
		ConcurrentDictionary<uint, bool> botCache = OwnershipCache.GetOrAdd(
			bot.BotName,
			static _ => new ConcurrentDictionary<uint, bool>()
		);

		botCache[appId] = result;
	}

	internal static void RemoveBotCache(Bot bot) => OwnershipCache.TryRemove(bot.BotName, out _);
	#endregion
}
