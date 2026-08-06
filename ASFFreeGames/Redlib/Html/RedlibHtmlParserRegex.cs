using System.Text.RegularExpressions;

namespace Maxisoft.ASF.Redlib.Html;

#pragma warning disable CA1052

public partial class RedlibHtmlParserRegex {
	[GeneratedRegex(
		@"(.addlicense)\s+(asf)?\s*((?<appid>(s/|a/)\d+)\s*,?\s*)+",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
	)]
	internal static partial Regex CommandRegex();

	[GeneratedRegex(
		@"href\s*=\s*.\s*/r/[\P{Cc}\P{Cn}\P{Cs}]+?comments[\P{Cc}\P{Cn}\P{Cs}/]+?.\s*/?\s*>.*",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
	)]
	internal static partial Regex HrefCommentLinkRegex();

	[GeneratedRegex(
		@".*free\s+DLC\s+for\s+a.*",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
	)]
	internal static partial Regex IsDlcRegex();

	[GeneratedRegex(
		@".*free\s+to\s+play.*",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
	)]
	internal static partial Regex IsFreeToPlayRegex();

	[GeneratedRegex(
		@".*permanently\s+free.*",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
	)]
	internal static partial Regex IsPermanentlyFreeRegex();
}

#pragma warning restore CA1052
