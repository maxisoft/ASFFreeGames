using System;
using System.Collections.Generic;
using System.Net;
using ArchiSteamFarm.Storage;
using ASFFreeGames.Configurations;

namespace Maxisoft.ASF.HttpClientSimple;

public sealed class SimpleHttpClientFactory(ASFFreeGamesOptions options) : IDisposable {
	private readonly HashSet<string> DisableProxyStrings = new(StringComparer.InvariantCultureIgnoreCase) {
		"no",
		"0",
		"false",
		"none",
		"disable",
		"disabled",
		"null",
		"off",
		"noproxy",
		"no-proxy"
	};

	private readonly Dictionary<ECacheKey, Tuple<IWebProxy?, SimpleHttpClient>> Cache = new();

	private enum ECacheKey {
		Generic,
		Reddit,
		Redlib,
		Github
	}

	private SimpleHttpClient CreateFor(ECacheKey key, string? proxy = null) {
		if (string.IsNullOrWhiteSpace(proxy)) {
			proxy = options.Proxy;
		}

		IWebProxy? webProxy;

		if (DisableProxyStrings.Contains(proxy ?? "")) {
			webProxy = null;
		}
		else if (!string.IsNullOrWhiteSpace(proxy)) {
			SimpleWebProxy simpleProxy = new(proxy, bypassOnLocal: true);

			if (Uri.TryCreate(proxy, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.UserInfo)) {
				string[] split = uri.UserInfo.Split(':');

				if (split.Length == 2) {
					simpleProxy.Credentials = new NetworkCredential(split[0], split[1]);
				}
			}

			webProxy = simpleProxy;
		}
		else {
			webProxy = ArchiSteamFarm.Core.ASF.GlobalConfig?.WebProxy;
		}

		lock (Cache) {
			if (Cache.TryGetValue(key, out Tuple<IWebProxy?, SimpleHttpClient>? cached)) {
				// Use a test URI to get proxy addresses via IWebProxy interface
				// This avoids direct WebProxy type references which cause runtime issues
				Uri testUri = new("http://test.example.com", UriKind.Absolute);
				Uri? cachedAddress = cached.Item1?.GetProxy(testUri);
				Uri? currentAddress = webProxy?.GetProxy(testUri);

				if (cachedAddress == currentAddress) {
					return cached.Item2;
				}

				Cache.Remove(key);
			}

#pragma warning disable CA2000
			Tuple<IWebProxy?, SimpleHttpClient> tuple = new(webProxy, new SimpleHttpClient(webProxy));
#pragma warning restore CA2000
			Cache.Add(key, tuple);

			return tuple.Item2;
		}
	}

	public SimpleHttpClient CreateForReddit() => CreateFor(ECacheKey.Reddit, options.RedditProxy ?? options.Proxy);
	public SimpleHttpClient CreateForRedlib() => CreateFor(ECacheKey.Redlib, options.RedlibProxy ?? options.RedditProxy ?? options.Proxy);
	public SimpleHttpClient CreateForGithub() => CreateFor(ECacheKey.Github, options.Proxy);

	public SimpleHttpClient CreateGeneric() => CreateFor(ECacheKey.Generic, options.Proxy);

	public void Dispose() {
		lock (Cache) {
			foreach ((_, (_, SimpleHttpClient? item2)) in Cache) {
				item2.Dispose();
			}

			Cache.Clear();
		}
	}
}
