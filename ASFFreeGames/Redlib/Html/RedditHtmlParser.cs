using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using ASFFreeGames.ASFExtentions.Games;
using Maxisoft.ASF.Reddit;
using Maxisoft.Utils.Collections.Dictionaries;

namespace Maxisoft.ASF.Redlib.Html;

public static class RedlibHtmlParser {
	private const int MaxIdentifierPerEntry = 32;

	public static IReadOnlyCollection<RedlibGameEntry> ParseGamesFromHtml(ReadOnlySpan<char> html, bool dedup = true) {
		Maxisoft.Utils.Collections.Dictionaries.OrderedDictionary<RedlibGameEntry, EmptyStruct> entries = new(dedup ? new GameIdentifiersEqualityComparer() : EqualityComparer<RedlibGameEntry>.Default);
		int startIndex = 0;

		Span<GameIdentifier> gameIdentifiers = stackalloc GameIdentifier[MaxIdentifierPerEntry];

		while ((0 <= startIndex) && (startIndex < html.Length)) {
			ParserIndices indices;

			try {
				indices = ParseIndices(html, startIndex);

				(int startOfCommandIndex, int endOfCommandIndex, int _, _, _, _, _) = indices;

				ReadOnlySpan<char> command = html[startOfCommandIndex..endOfCommandIndex].Trim();

				if (!RedlibHtmlParserRegex.CommandRegex().IsMatch(command)) {
					throw new SkipAndContinueParsingException("Invalid asf command") { StartIndex = startOfCommandIndex + 1 };
				}

				Span<GameIdentifier> effectiveGameIdentifiers = SplitCommandAndGetGameIdentifiers(command, gameIdentifiers);

				if (effectiveGameIdentifiers.IsEmpty) {
					throw new SkipAndContinueParsingException("No game identifiers found") { StartIndex = startOfCommandIndex + 1 };
				}

				EGameType flag = ParseGameTypeFlags(html[indices.StartOfCommandIndex..indices.StartOfFooterIndex]);

				ReadOnlySpan<char> title = ExtractTitle(html, indices);

				DateTimeOffset createdDate = default;

				if ((indices.DateStartIndex < indices.DateEndIndex) && (indices.DateEndIndex > 0)) {
					ReadOnlySpan<char> dateString = html[indices.DateStartIndex..indices.DateEndIndex].Trim();

					if (!TryParseCreatedDate(dateString, out createdDate)) {
						createdDate = default(DateTimeOffset);
					}
				}

				RedlibGameEntry entry = new(effectiveGameIdentifiers.ToArray(), title.ToString(), flag, createdDate);

				try {
					entries.Add(entry, default(EmptyStruct));
				}
				catch (ArgumentException e) {
					throw new SkipAndContinueParsingException("entry already found", e) { StartIndex = startOfCommandIndex + 1 };
				}
			}
			catch (SkipAndContinueParsingException e) {
				startIndex = e.StartIndex;

				continue;
			}

			startIndex = indices.StartOfFooterIndex + 1;
		}

		return (IReadOnlyCollection<RedlibGameEntry>) entries.Keys;
	}

	private static readonly string[] CommonDateFormat = ["MM dd yyyy, HH:mm:ss zzz", "MM dd yyyy, HH:mm:ss zzz", "MMM dd yyyy, HH:mm:ss UTC", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss zzz", "yyyy-MM-dd HH:mm:ss.fffffff zzz", "yyyy-MM-ddTHH:mm:ss.fffffffzzz", "yyyy-MM-dd HH:mm:ss", "yyyyMMddHHmmss", "yyyyMMddHHmmss.fffffff"];

	private static bool TryParseCreatedDate(ReadOnlySpan<char> dateString, out DateTimeOffset createdDate) {
		// parse date like May 31 2024, 12:28:53 UTC

		if (dateString.IsEmpty) {
			createdDate = DateTimeOffset.Now;

			return false;
		}

		foreach (string format in CommonDateFormat) {
			if (DateTimeOffset.TryParseExact(dateString, format, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out createdDate)) {
				return true;
			}
		}

		if (DateTimeOffset.TryParse(dateString, DateTimeFormatInfo.InvariantInfo, out createdDate)) {
			return true;
		}

		createdDate = DateTimeOffset.Now;

		return false;
	}

	internal static ReadOnlySpan<char> ExtractTitle(ReadOnlySpan<char> html, ParserIndices indices) {
		Span<Range> ranges = stackalloc Range[MaxIdentifierPerEntry];
		ReadOnlySpan<char> hrefSpan = html[indices.HrefStartIndex..indices.HrefEndIndex];
		int splitCount = hrefSpan.Split(ranges, '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (splitCount > 2) {
			Range range = ranges[..splitCount][^3];

			return hrefSpan[range].Trim();
		}

		return ReadOnlySpan<char>.Empty;
	}

	internal static EGameType ParseGameTypeFlags(ReadOnlySpan<char> content) {
		EGameType flag = EGameType.None;

		if (RedlibHtmlParserRegex.IsDlcRegex().IsMatch(content)) {
			flag |= EGameType.Dlc;
		}

		if (RedlibHtmlParserRegex.IsPermanentlyFreeRegex().IsMatch(content)) {
			flag |= EGameType.PermenentlyFree;
		}

		if (RedlibHtmlParserRegex.IsFreeToPlayRegex().IsMatch(content)) {
			flag |= EGameType.FreeToPlay;
		}

		return flag;
	}

	internal static ParserIndices ParseIndices(ReadOnlySpan<char> html, int start) {
		// Find the index of the next !addlicense asf command
		int startIndex = html[start..].IndexOf("<code>!addlicense asf ", StringComparison.OrdinalIgnoreCase);

		if (startIndex < 0) {
			startIndex = html[start..].IndexOf("<pre>!addlicense asf ", StringComparison.OrdinalIgnoreCase);

			if (startIndex < 0) {
				throw new SkipAndContinueParsingException("No !addlicense asf command found") { StartIndex = -1 };
			}
		}

		startIndex += start;

		int commentLinkIndex = html[start..startIndex].LastIndexOf("<a class=\"comment_link\"", StringComparison.InvariantCultureIgnoreCase);

		if (commentLinkIndex < 0) {
			throw new SkipAndContinueParsingException("No comment link found") { StartIndex = startIndex + 1 };
		}

		commentLinkIndex += start;

		int createdStartIndex = html[commentLinkIndex..startIndex].IndexOf("<span class=\"created\"", StringComparison.InvariantCultureIgnoreCase);

		if (createdStartIndex < 0) {
			throw new SkipAndContinueParsingException("No created span found") { StartIndex = startIndex + 1 };
		}

		createdStartIndex += commentLinkIndex;

		const string title = "title=\"";
		int createdTitleStartIndex = html[createdStartIndex..startIndex].IndexOf(title, StringComparison.InvariantCultureIgnoreCase);

		if (createdTitleStartIndex < 0) {
			throw new SkipAndContinueParsingException("No created title attribute found") { StartIndex = startIndex + 1 };
		}

		createdTitleStartIndex += createdStartIndex + title.Length;

		int createdTitleEndIndex = html[createdTitleStartIndex..startIndex].IndexOf("\"", StringComparison.InvariantCultureIgnoreCase);

		if (createdTitleEndIndex < 0) {
			throw new SkipAndContinueParsingException("No created title attribute end found") { StartIndex = startIndex + 1 };
		}

		createdTitleEndIndex += createdTitleStartIndex;

		int hrefStartIndex = html[commentLinkIndex..startIndex].IndexOf("href", StringComparison.InvariantCultureIgnoreCase);

		if (hrefStartIndex < 0) {
			throw new SkipAndContinueParsingException("No comment href found") { StartIndex = startIndex + 1 };
		}

		hrefStartIndex += commentLinkIndex;

		int hrefEndIndex = hrefStartIndex + 1024;
		hrefEndIndex = Math.Min(hrefEndIndex, html.Length);
		hrefEndIndex = html[hrefStartIndex..hrefEndIndex].IndexOf('>');

		if (hrefEndIndex < 0) {
			throw new SkipAndContinueParsingException("No comment href end found") { StartIndex = startIndex + 1 };
		}

		hrefEndIndex += hrefStartIndex;

		if (!RedlibHtmlParserRegex.HrefCommentLinkRegex().IsMatch(html[hrefStartIndex..(hrefEndIndex + 1)])) {
			throw new SkipAndContinueParsingException("Invalid comment link") { StartIndex = startIndex + 1 };
		}

		// Find the ASF info bot footer
		int footerStartIndex = html[startIndex..].IndexOf("bot", StringComparison.InvariantCultureIgnoreCase);

		if (footerStartIndex < 0) {
			throw new SkipAndContinueParsingException("No bot in footer found") { StartIndex = startIndex + 1 };
		}

		footerStartIndex += startIndex;

		int infoFooterStartIndex = html[footerStartIndex..].IndexOf("Info", StringComparison.InvariantCultureIgnoreCase);

		if (infoFooterStartIndex < 0) {
			throw new SkipAndContinueParsingException("No Info in footer found") { StartIndex = startIndex + 1 };
		}

		infoFooterStartIndex += footerStartIndex;

		// now we have a kind of typical ASFInfo post

		// Extract the comment link
		int commandEndIndex = html[startIndex..infoFooterStartIndex].IndexOf("</code>", StringComparison.InvariantCultureIgnoreCase);

		if (commandEndIndex < 0) {
			commandEndIndex = html[startIndex..infoFooterStartIndex].IndexOf("</pre>", StringComparison.InvariantCultureIgnoreCase);

			if (commandEndIndex < 0) {
				throw new SkipAndContinueParsingException("No command end found") { StartIndex = startIndex + 1 };
			}
		}

		commandEndIndex += startIndex;

		startIndex = html[startIndex..commandEndIndex].IndexOf("!addlicense", StringComparison.OrdinalIgnoreCase) + startIndex;

		return new ParserIndices(startIndex, commandEndIndex, infoFooterStartIndex, hrefStartIndex, hrefEndIndex, createdTitleStartIndex, createdTitleEndIndex);
	}

	internal static Span<GameIdentifier> SplitCommandAndGetGameIdentifiers(ReadOnlySpan<char> command, Span<GameIdentifier> gameIdentifiers) {
		Span<Range> ranges = stackalloc Range[MaxIdentifierPerEntry];
		int splits = command.Split(ranges, ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (splits <= 0) {
			return Span<GameIdentifier>.Empty;
		}

		// fix the first range because it contains the command
		ref Range firstRange = ref ranges[0];
		int startFirstRange = command[firstRange].LastIndexOf(' ');
		firstRange = new Range(firstRange.Start.GetOffset(command.Length) + startFirstRange + 1, firstRange.End);

		int gameIdentifiersCount = 0;

		foreach (Range range in ranges[..splits]) {
			ReadOnlySpan<char> sub = command[range].Trim();

			if (sub.IsEmpty) {
				continue;
			}

			if (!GameIdentifier.TryParse(sub, out GameIdentifier gameIdentifier)) {
				continue;
			}

			gameIdentifiers[gameIdentifiersCount++] = gameIdentifier;
		}

		return gameIdentifiers[..gameIdentifiersCount];
	}
}
