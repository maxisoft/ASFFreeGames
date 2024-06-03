using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Maxisoft.ASF.Redlib.Html;

#pragma warning disable CA1052
public partial class RedlibHtmlParserRegex {
	[GeneratedRegex(@"(.addlicense)\s+(asf)?\s*((?<appid>(s/|a/)\d+)\s*,?\s*)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex CommandRegex();

	[GeneratedRegex(@"href\s*=\s*.\s*/r/[\P{Cc}\P{Cn}\P{Cs}]+?comments[\P{Cc}\P{Cn}\P{Cs}/]+?.\s*/?\s*>.*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex HrefCommentLinkRegex();

	[GeneratedRegex(@".*free\s+DLC\s+for\s+a.*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex IsDlcRegex();

	[GeneratedRegex(@".*free\s+to\s+play.*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex IsFreeToPlayRegex();

	[GeneratedRegex(@".*permanently\s+free.*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex IsPermanentlyFreeRegex();
#pragma warning restore CA1052
}

public class SkipAndContinueParsingException : Exception {
	public int StartIndex { get; init; }

	public SkipAndContinueParsingException(string message, Exception innerException) : base(message, innerException) { }

	public SkipAndContinueParsingException() { }

	public SkipAndContinueParsingException(string message) : base(message) { }
}

internal readonly record struct ParserIndices(int StartOfCommandIndex, int EndOfCommandIndex, int StartOfFooterIndex, int HrefStartIndex, int HrefEndIndex);

public static class RedlibHtmlParser {
	private const int MaxIdentifierPerEntry = 32;

	public static IReadOnlyList<GameEntry> ParseGamesFromHtml(ReadOnlySpan<char> html) {
		List<GameEntry> entries = [];
		int startIndex = 0;

		Span<GameIdentifier> gameIdentifiers = stackalloc GameIdentifier[MaxIdentifierPerEntry];

		while ((0 <= startIndex) && (startIndex < html.Length)) {
			ParserIndices indices;

			try {
				indices = ParseIndices(html, startIndex);

				(int startOfCommandIndex, int endOfCommandIndex, int _, _, _) = indices;

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
				entries.Add(new GameEntry(effectiveGameIdentifiers.ToArray(), title.ToString(), flag));
			}
			catch (SkipAndContinueParsingException e) {
				startIndex = e.StartIndex;

				continue;
			}

			startIndex = indices.StartOfFooterIndex + 1;
		}

		return entries;
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

		// Extract the comment link and validate the entry
		int commandEndIndex = html[startIndex..infoFooterStartIndex].IndexOf("</code>", StringComparison.InvariantCultureIgnoreCase);

		if (commandEndIndex < 0) {
			commandEndIndex = html[startIndex..infoFooterStartIndex].IndexOf("</pre>", StringComparison.InvariantCultureIgnoreCase);

			if (commandEndIndex < 0) {
				throw new SkipAndContinueParsingException("No command end found") { StartIndex = startIndex + 1 };
			}
		}

		commandEndIndex += startIndex;

		startIndex = html[startIndex..commandEndIndex].IndexOf("!addlicense", StringComparison.OrdinalIgnoreCase) + startIndex;

		return new ParserIndices(startIndex, commandEndIndex, infoFooterStartIndex, hrefStartIndex, hrefEndIndex);
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

			Debug.Assert(gameIdentifiersCount < gameIdentifiers.Length);
			gameIdentifiers[gameIdentifiersCount++] = gameIdentifier;
		}

		return gameIdentifiers[..gameIdentifiersCount];
	}
}

#pragma warning disable CA1819
public readonly record struct GameEntry(IReadOnlyCollection<GameIdentifier> GameIdentifiers, string CommentLink, EGameType TypeFlags) { }
#pragma warning restore CA1819

[Flags]
public enum EGameType : sbyte {
	None = 0,
	FreeToPlay = 1 << 0,
	PermenentlyFree = 1 << 1,
	Dlc = 1 << 2
}
