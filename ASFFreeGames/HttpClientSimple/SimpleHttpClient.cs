using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Maxisoft.ASF.HttpClientSimple;

#nullable enable

public sealed class SimpleHttpClient : IDisposable {
	private readonly HttpClientHandler HttpClientHandler;
	private readonly HttpClient HttpClient;

	public SimpleHttpClient(IWebProxy? proxy = null, long timeout = 25_000) {
		HttpClientHandler = new HttpClientHandler {
			AutomaticDecompression = DecompressionMethods.All,
		};

		if (proxy is not null) {
			HttpClientHandler.Proxy = proxy;
			HttpClientHandler.UseProxy = true;

			if (proxy.Credentials is not null) {
				HttpClientHandler.PreAuthenticate = true;
			}
		}

		HttpClient = new HttpClient(HttpClientHandler, false) { Timeout = TimeSpan.FromMilliseconds(timeout) };
		HttpClient.DefaultRequestVersion = HttpVersion.Version30;
		HttpClient.DefaultRequestHeaders.ExpectContinue = false;

		HttpClient.DefaultRequestHeaders.Add("User-Agent", "Lynx/2.8.8dev.9 libwww-FM/2.14 SSL-MM/1.4.1 GNUTLS/2.12.14");
		HttpClient.DefaultRequestHeaders.Add("DNT", "1");
		HttpClient.DefaultRequestHeaders.Add("Sec-GPC", "1");

		HttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
		HttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.8));
	}

	public async Task<HttpStreamResponse> GetStreamAsync(Uri uri, IEnumerable<KeyValuePair<string, string>>? additionalHeaders = null, CancellationToken cancellationToken = default) {
		using HttpRequestMessage request = new(HttpMethod.Get, uri);
		request.Version = HttpClient.DefaultRequestVersion;

		// Add additional headers if provided
		if (additionalHeaders != null) {
			foreach (KeyValuePair<string, string> header in additionalHeaders) {
				request.Headers.Add(header.Key, header.Value);
			}
		}

		HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
		Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

		return new HttpStreamResponse(response, stream);
	}

	public void Dispose() {
		HttpClient.Dispose();
		HttpClientHandler.Dispose();
	}
}

public sealed class HttpStreamResponse(HttpResponseMessage response, Stream stream) : IAsyncDisposable {
	public HttpResponseMessage Response { get; } = response;
	public Stream Stream { get; } = stream;

	public async Task<string> ReadAsStringAsync(CancellationToken cancellationToken) {
		using StreamReader reader = new(Stream, Encoding.UTF8);

		return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
	}

	public HttpStatusCode StatusCode => Response.StatusCode;

	public async ValueTask DisposeAsync() {
		ConfiguredValueTaskAwaitable task = Stream.DisposeAsync().ConfigureAwait(false);
		Response.Dispose();
		await task;
	}
}
