using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ArchiSteamFarm.NLog;
using ArchiSteamFarm.Steam;
using NLog;
using NLog.Config;
using NLog.Filters;

namespace Maxisoft.ASF;
#nullable enable

public class LoggerFilter {
	private static readonly Lazy<Regex> AddLicenceCommonErrorsRegex = new(static () => new Regex(@"^.*?InternalRequest(?>\s*)\(\w*?\)(?>\s*)(?:(?:InternalServerError)|(?:Forbidden)).*?$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

	private readonly ConcurrentDictionary<string, LinkedList<Func<LogEventInfo, bool>>> Filters = new();
	private readonly MarkedWhenMethodFilter MethodFilter;

	public LoggerFilter() => MethodFilter = new MarkedWhenMethodFilter(FilterLogEvent);

	public IDisposable DisableLogging(Func<LogEventInfo, bool> filter, [NotNull] Bot bot) {
		Logger logger = GetLogger(bot.ArchiLogger, bot.BotName);

		LinkedList<Func<LogEventInfo, bool>>? filters;

		lock (Filters) {
			Filters.TryGetValue(bot.BotName, out filters);

			if (filters is null) {
				filters = new LinkedList<Func<LogEventInfo, bool>>();

				if (!Filters.TryAdd(bot.BotName, filters)) {
					filters = Filters[bot.BotName];
				}
			}

			LinkedListNode<Func<LogEventInfo, bool>> node = filters.AddLast(filter);
			LoggingConfiguration? config = logger.Factory.Configuration;

			bool reconfig = false;

			foreach (LoggingRule loggingRule in config.LoggingRules.Where(loggingRule => !loggingRule.Filters.Any(f => ReferenceEquals(f, MethodFilter)))) {
				loggingRule.Filters.Insert(0, MethodFilter);
				reconfig = true;
			}

			if (reconfig) {
				logger.Factory.ReconfigExistingLoggers();
			}

			return new LoggerRemoveFilterDisposable(node);
		}
	}

	public IDisposable DisableLoggingForAddLicenseCommonErrors(Func<LogEventInfo, bool> filter, [NotNull] Bot bot) {
		bool filter2(LogEventInfo info) => (info.Level == LogLevel.Debug) && filter(info) && AddLicenceCommonErrorsRegex.Value.IsMatch(info.Message);

		return DisableLogging(filter2, bot);
	}

	private FilterResult FilterLogEvent(LogEventInfo eventInfo) {
		Bot? bot = eventInfo.LoggerName == "ASF" ? null : Bot.GetBot(eventInfo.LoggerName ?? "");

		if (Filters.TryGetValue(bot?.BotName ?? eventInfo.LoggerName ?? "", out LinkedList<Func<LogEventInfo, bool>>? filters)) {
			return filters.Any(func => func(eventInfo)) ? FilterResult.IgnoreFinal : FilterResult.Log;
		}

		return FilterResult.Log;
	}

	private static Logger GetLogger(ArchiLogger logger, string name = "ASF") {
		FieldInfo? field = logger.GetType().GetField("Logger", BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty);

		return field?.GetValue(logger) as Logger ?? LogManager.GetLogger(name);
	}

	private sealed class LoggerRemoveFilterDisposable : IDisposable {
		private readonly LinkedListNode<Func<LogEventInfo, bool>> Node;

		public LoggerRemoveFilterDisposable(LinkedListNode<Func<LogEventInfo, bool>> node) => Node = node;

		public void Dispose() => Node.List?.Remove(Node);
	}

	private class MarkedWhenMethodFilter : WhenMethodFilter {
		public MarkedWhenMethodFilter(Func<LogEventInfo, FilterResult> filterMethod) : base(filterMethod) { }
	}

	private bool RemoveFilters(string botName) => Filters.TryRemove(botName, out _);
	public bool RemoveFilters(Bot? bot) => bot is not null && RemoveFilters(bot.BotName);
}
