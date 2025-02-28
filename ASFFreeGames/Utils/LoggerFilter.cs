using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ArchiSteamFarm.NLog;
using ArchiSteamFarm.Steam;
using ASFFreeGames.ASFExtensions.Bot;
using Maxisoft.ASF.ASFExtensions;
using NLog;
using NLog.Config;
using NLog.Filters;

// ReSharper disable RedundantNullableFlowAttribute

namespace Maxisoft.ASF.Utils;

#nullable enable

/// <summary>
/// Represents a class that provides methods for filtering log events based on custom criteria.
/// </summary>
public partial class LoggerFilter {
    // A concurrent dictionary that maps bot names to lists of filter functions
    private readonly ConcurrentDictionary<BotName, LinkedList<Func<LogEventInfo, bool>>> Filters = new();

    // A custom filter that invokes the FilterLogEvent method
    private readonly MarkedWhenMethodFilter MethodFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerFilter"/> class.
    /// </summary>
    public LoggerFilter() => MethodFilter = new MarkedWhenMethodFilter(FilterLogEvent);

    /// <summary>
    /// Disables logging for a specific bot based on a filter function.
    /// </summary>
    /// <param name="filter">The filter function that determines whether to ignore a log event.</param>
    /// <param name="bot">The bot instance whose logging should be disabled.</param>
    /// <returns>A disposable object that can be used to re-enable logging when disposed.</returns>
    public IDisposable DisableLogging(Func<LogEventInfo, bool> filter, [NotNull] Bot bot) {
        Logger logger = GetLogger(bot.ArchiLogger, bot.BotName);

        lock (Filters) {
            Filters.TryGetValue(bot.BotName, out LinkedList<Func<LogEventInfo, bool>>? filters);

            if (filters is null) {
                filters = new LinkedList<Func<LogEventInfo, bool>>();

                if (!Filters.TryAdd(bot.BotName, filters)) {
                    filters = Filters[bot.BotName];
                }
            }

            LinkedListNode<Func<LogEventInfo, bool>> node = filters.AddLast(filter);
            LoggingConfiguration? config = logger.Factory.Configuration;

            bool reconfigure = false;

            foreach (LoggingRule loggingRule in config.LoggingRules.Where(loggingRule => !loggingRule.Filters.Any(f => ReferenceEquals(f, MethodFilter)))) {
                loggingRule.Filters.Insert(0, MethodFilter);
                reconfigure = true;
            }

            if (reconfigure) {
                logger.Factory.ReconfigExistingLoggers();
            }

            return new LoggerRemoveFilterDisposable(node);
        }
    }

    /// <summary>
    /// Disables logging for a specific bot based on a filter function and a regex pattern for common errors when adding licenses.
    /// </summary>
    /// <param name="filter">The filter function that determines whether to ignore a log event.</param>
    /// <param name="bot">The bot instance whose logging should be disabled.</param>
    /// <returns>A disposable object that can be used to re-enable logging when disposed.</returns>
    public IDisposable DisableLoggingForAddLicenseCommonErrors(Func<LogEventInfo, bool> filter, [NotNull] Bot bot) {
        bool filter2(LogEventInfo info) => (info.Level == LogLevel.Debug) && filter(info) && AddLicenseCommonErrorsRegex().IsMatch(info.Message);

        return DisableLogging(filter2, bot);
    }

    /// <summary>
    /// Removes all filters for a specific bot.
    /// </summary>
    /// <param name="bot">The bot instance whose filters should be removed.</param>
    /// <returns>True if the removal was successful; otherwise, false.</returns>
    public bool RemoveFilters(Bot? bot) => bot is not null && RemoveFilters(bot.BotName);

    // A regex pattern for common errors when adding licenses
    [GeneratedRegex(@"^.*?InternalRequest(?>\s*)\(\w*?\)(?>\s*)(?:(?:InternalServerError)|(?:Forbidden)).*?$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AddLicenseCommonErrorsRegex();

    // A method that filters log events based on the registered filter functions
    private FilterResult FilterLogEvent(LogEventInfo eventInfo) {
        Bot? bot = eventInfo.LoggerName == "ASF" ? null : Bot.GetBot(eventInfo.LoggerName ?? "");

        if (Filters.TryGetValue(bot?.BotName ?? eventInfo.LoggerName ?? "", out LinkedList<Func<LogEventInfo, bool>>? filters)) {
            return filters.Any(func => func(eventInfo)) ? FilterResult.IgnoreFinal : FilterResult.Log;
        }

        return FilterResult.Log;
    }

    // A method that gets the logger instance from the ArchiLogger instance using introspection
    private static Logger GetLogger(ArchiLogger logger, string name = "ASF") {
        FieldInfo? field = logger.GetType().GetField("Logger", BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty);

        // Check if the field is null or the value is not a Logger instance
        return field?.GetValue(logger) is not Logger loggerInstance
            ?

            // Return a default logger with the given name
            LogManager.GetLogger(name)
            :

            // Return the logger instance from the field
            loggerInstance;
    }

    // A method that removes filters by bot name
    private bool RemoveFilters(BotName botName) => Filters.TryRemove(botName, out _);

    // A class that implements a disposable object for removing filters
    private sealed class LoggerRemoveFilterDisposable(LinkedListNode<Func<LogEventInfo, bool>> node) : IDisposable {
        public void Dispose() => node.List?.Remove(node);
    }

    // A class that implements a custom filter that invokes a method
    private class MarkedWhenMethodFilter(Func<LogEventInfo, FilterResult> filterMethod) : WhenMethodFilter(filterMethod);
}
