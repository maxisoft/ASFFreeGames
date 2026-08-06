using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ASFFreeGames.Configurations;
using Maxisoft.ASF.HttpClientSimple;
using Maxisoft.ASF.Reddit;

#nullable enable

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.Redlib.Instances;

[SuppressMessage("ReSharper", "RedundantNullableFlowAttribute")]
public class RedlibInstanceList(ASFFreeGamesOptions options) : IRedlibInstanceList {
	private const string EmbeddedFileName = "redlib_instances.json";

	private static readonly HashSet<string> DisabledKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"disabled",
		"off",
		"no",
		"false",
	};

	private readonly ASFFreeGamesOptions Options =
		options ?? throw new ArgumentNullException(nameof(options));

	public async Task<List<Uri>> ListInstances(
		[NotNull] SimpleHttpClient httpClient,
		CancellationToken cancellationToken
	) {
		if (IsDisabled(Options.RedlibInstanceUrl)) {
			throw new RedlibDisabledException();
		}

		if (!Uri.TryCreate(Options.RedlibInstanceUrl, UriKind.Absolute, out Uri? uri)) {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericError(
				"[FreeGames] Invalid redlib instances url: " + Options.RedlibInstanceUrl
			);

			return await ListFromEmbedded(cancellationToken).ConfigureAwait(false);
		}

#pragma warning disable CAC001
#pragma warning disable CA2007
		await using HttpStreamResponse response = await httpClient
			.GetStreamAsync(uri, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
#pragma warning restore CA2007
#pragma warning restore CAC001

		if (!response.StatusCode.IsSuccessCode()) {
			return await ListFromEmbedded(cancellationToken).ConfigureAwait(false);
		}

		JsonNode? node = await ParseJsonNode(response, cancellationToken).ConfigureAwait(false);

		if (node is null) {
			return await ListFromEmbedded(cancellationToken).ConfigureAwait(false);
		}

		CheckUpToDate(node);

		List<Uri> res = ParseUrls(node);

		return res.Count > 0
			? res
			: await ListFromEmbedded(cancellationToken).ConfigureAwait(false);
	}

	internal static void CheckUpToDate(JsonNode node) {
		int currentYear = DateTime.Now.Year;
		string updated = node["updated"]?.GetValue<string>() ?? "";

		if (
			!updated.StartsWith(
				currentYear.ToString(CultureInfo.InvariantCulture),
				StringComparison.Ordinal
			)
			&& !updated.StartsWith(
				(currentYear - 1).ToString(CultureInfo.InvariantCulture),
				StringComparison.Ordinal
			)
		) {
			throw new RedlibOutDatedListException();
		}
	}

	internal static async Task<List<Uri>> ListFromEmbedded(CancellationToken cancellationToken) {
		JsonNode? node = await LoadEmbeddedInstance(cancellationToken).ConfigureAwait(false);

		if (node is null) {
#pragma warning disable CA2201
			throw new NullReferenceException($"unable to find embedded file {EmbeddedFileName}");
#pragma warning restore CA2201
		}

		CheckUpToDate(node);

		return ParseUrls(node);
	}

	internal static List<Uri> ParseUrls(JsonNode json) {
		JsonNode? instances = json["instances"];

		if (instances is null) {
			return [];
		}

		List<Uri> uris = new(((JsonArray) instances).Count);

		// ReSharper disable once LoopCanBePartlyConvertedToQuery
		foreach (JsonNode? instance in (JsonArray) instances) {
			JsonNode? url = instance?["url"];

			if (
				Uri.TryCreate(url?.GetValue<string>() ?? "", UriKind.Absolute, out Uri? instanceUri)
				&& instanceUri.Scheme is "http" or "https"
			) {
				uris.Add(instanceUri);
			}
		}

		return uris;
	}

	private static bool IsDisabled(string? instanceUrl) =>
		instanceUrl is not null && DisabledKeywords.Contains(instanceUrl.Trim());

	private static async Task<JsonNode?> LoadEmbeddedInstance(CancellationToken cancellationToken) {
		Assembly assembly = Assembly.GetExecutingAssembly();

#pragma warning disable CAC001
#pragma warning disable CA2007
		await using Stream stream = assembly.GetManifestResourceStream(
			$"{assembly.GetName().Name}.Resources.{EmbeddedFileName}"
		)!;
#pragma warning restore CA2007
#pragma warning restore CAC001

		using StreamReader reader = new(stream); // assume the encoding is UTF8, cannot be specified as per issue #91
		string data = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

		return JsonNode.Parse(data);
	}

	private static Task<JsonNode?> ParseJsonNode(
		HttpStreamResponse stream,
		CancellationToken cancellationToken
	) => RedditHelper.ParseJsonNode(stream, cancellationToken);
}
