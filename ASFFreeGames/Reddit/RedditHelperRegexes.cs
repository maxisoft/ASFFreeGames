using System.Text.RegularExpressions;

namespace Maxisoft.ASF.Reddit;

internal static partial class RedditHelperRegexes {
	[GeneratedRegex(@"(.addlicense)\s+(asf)?\s*((?<appid>(s/|a/)\d+)\s*,?\s*)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex Command();

	[GeneratedRegex(@"free\s+DLC\s+for\s+a", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex IsDlc();

	[GeneratedRegex(@"permanently\s+free", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex IsPermanentlyFree();
}
