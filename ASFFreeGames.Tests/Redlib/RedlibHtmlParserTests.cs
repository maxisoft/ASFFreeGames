using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Maxisoft.ASF.Redlib;
using Maxisoft.ASF.Redlib.Html;
using Xunit;

namespace Maxisoft.ASF.Tests.Redlib;

public class RedlibHtmlParserTests {
	[Fact]
	public async void Test() {
		string html = await LoadHtml().ConfigureAwait(false);

		// ReSharper disable once ArgumentsStyleLiteral
		IReadOnlyCollection<RedlibGameEntry> result = RedlibHtmlParser.ParseGamesFromHtml(html, dedup: false);
		Assert.NotEmpty(result);
		Assert.Equal(25, result.Count);

// ReSharper disable once ArgumentsStyleLiteral
		result = RedlibHtmlParser.ParseGamesFromHtml(html, dedup: true);
		Assert.NotEmpty(result);
		Assert.Equal(13, result.Count);
	}

	private static async Task<string> LoadHtml() {
		Assembly assembly = Assembly.GetExecutingAssembly();

#pragma warning disable CA2007
		await using Stream stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.redlib_asfinfo.html")!;
#pragma warning restore CA2007
		using StreamReader reader = new(stream, Encoding.UTF8, true);

		return await reader.ReadToEndAsync().ConfigureAwait(false);
	}
}
