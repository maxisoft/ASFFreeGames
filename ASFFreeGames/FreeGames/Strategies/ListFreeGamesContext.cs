using System;
using ASFFreeGames.Configurations;
using Maxisoft.ASF.HttpClientSimple;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.FreeGames.Strategies;

public sealed record ListFreeGamesContext(ASFFreeGamesOptions Options, Lazy<SimpleHttpClient> HttpClient, uint Retry = 5) {
	public required SimpleHttpClientFactory HttpClientFactory { get; init; }
	public EListFreeGamesStrategy PreviousSucessfulStrategy { get; set; }

	public required IListFreeGamesStrategy Strategy { get; init; }
}
