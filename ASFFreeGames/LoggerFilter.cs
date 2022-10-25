using System;
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
	private static readonly Lazy<Regex> AddKeyCommonErrorsRegex = new(new Regex(@".*InternalRequest\s*\(\w*?\)\s*(?:(?:InternalServerError)|(?:Forbidden)).*$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

	private readonly ConditionalWeakTable<Bot, LinkedList<Func<LogEventInfo, bool>>> Filters = new();
	private readonly MarkedWhenMethodFilter MethodFilter;

	public LoggerFilter() => MethodFilter = new MarkedWhenMethodFilter(FilterLogEvent);

	public IDisposable DisableLogging(Func<LogEventInfo, bool> filter, [NotNull] Bot bot) {
		Logger logger = GetLogger(bot.ArchiLogger, bot.BotName);

		LinkedList<Func<LogEventInfo, bool>>? filters;

		lock (Filters) {
			Filters.TryGetValue(bot, out filters);

			if (filters is null) {
				filters = new LinkedList<Func<LogEventInfo, bool>>();
				Filters.Add(bot, filters);
			}

			LinkedListNode<Func<LogEventInfo, bool>> node = filters.AddLast(filter);
			LoggingConfiguration? config = logger.Factory.Configuration;

			bool reconfig = false;

			foreach (LoggingRule loggingRule in config.LoggingRules.Where(loggingRule => !loggingRule.Filters.Contains(MethodFilter))) {
				loggingRule.Filters.Insert(0, MethodFilter);
				reconfig = true;
			}

			if (reconfig) {
				logger.Factory.ReconfigExistingLoggers();
			}

			return new LoggerRemoveFilterDisposable(bot, node, Filters);
		}
	}

	public IDisposable DisableLoggingForAddKeyCommonErrors(Func<LogEventInfo, bool> filter, [NotNull] Bot bot) {
		bool filter2(LogEventInfo info) => (info.Level == LogLevel.Debug) && AddKeyCommonErrorsRegex.Value.IsMatch(info.Message) && filter(info);

		return DisableLogging(filter2, bot);
	}

	private FilterResult FilterLogEvent(LogEventInfo eventInfo) {
		Bot? bot = eventInfo.LoggerName == "ASF" ? null : Bot.GetBot(eventInfo.LoggerName ?? "");

		if (bot is not null && Filters.TryGetValue(bot, out LinkedList<Func<LogEventInfo, bool>>? filters)) {
			return filters.Any(func => func(eventInfo)) ? FilterResult.IgnoreFinal : FilterResult.Neutral;
		}

		return FilterResult.Neutral;
	}

	private static Logger GetLogger(ArchiLogger logger, string name = "ASF") {
		FieldInfo? field = logger.GetType().GetField("Logger", BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty);

		return field?.GetValue(logger) as Logger ?? LogManager.GetLogger(name);
	}

	private sealed class LoggerRemoveFilterDisposable : IDisposable {
		private readonly Bot Bot;
		private readonly ConditionalWeakTable<Bot, LinkedList<Func<LogEventInfo, bool>>> Filters;
		private readonly LinkedListNode<Func<LogEventInfo, bool>> Node;

		public LoggerRemoveFilterDisposable(Bot bot, LinkedListNode<Func<LogEventInfo, bool>> node, ConditionalWeakTable<Bot, LinkedList<Func<LogEventInfo, bool>>> filters) {
			Bot = bot;
			Node = node;
			Filters = filters;
		}

		public void Dispose() {
			if (Filters.TryGetValue(Bot, out LinkedList<Func<LogEventInfo, bool>>? list)) {
				list?.Remove(Node);
			}
		}
	}

	private class MarkedWhenMethodFilter : WhenMethodFilter {
		public MarkedWhenMethodFilter(Func<LogEventInfo, FilterResult> filterMethod) : base(filterMethod) { }
	}
}
