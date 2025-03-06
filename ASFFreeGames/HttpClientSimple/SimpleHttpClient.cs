using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Maxisoft.ASF.HttpClientSimple;

#nullable enable

public sealed class SimpleHttpClient : IDisposable {
	private readonly HttpMessageHandler HttpMessageHandler;
	private readonly HttpClient HttpClient;

	public SimpleHttpClient(IWebProxy? proxy = null, long timeout = 25_000) {
		SocketsHttpHandler handler = new();

		SetPropertyWithLogging(handler, nameof(SocketsHttpHandler.AutomaticDecompression), DecompressionMethods.All);
		SetPropertyWithLogging(handler, nameof(SocketsHttpHandler.MaxConnectionsPerServer), 5, debugLogLevel: true);
		SetPropertyWithLogging(handler, nameof(SocketsHttpHandler.EnableMultipleHttp2Connections), true);

		if (proxy is not null) {
			SetPropertyWithLogging(handler, nameof(SocketsHttpHandler.Proxy), proxy);
			SetPropertyWithLogging(handler, nameof(SocketsHttpHandler.UseProxy), true);

			if (proxy.Credentials is not null) {
				SetPropertyWithLogging(handler, nameof(SocketsHttpHandler.PreAuthenticate), true);
			}
		}

		HttpMessageHandler = handler;
#pragma warning disable CA5399
		HttpClient = new HttpClient(handler, false);
#pragma warning restore CA5399
		SetPropertyWithLogging(HttpClient, nameof(HttpClient.DefaultRequestVersion), HttpVersion.Version30);
		SetPropertyWithLogging(HttpClient, nameof(HttpClient.Timeout), TimeSpan.FromMilliseconds(timeout));

		SetExpectContinueProperty(HttpClient, false);

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
				request.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}
		}

		HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
		Stream? stream = null;

		try {
			stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception) {
			if (response.IsSuccessStatusCode) {
				throw; // something is wrong
			}

			// assume that the caller checks the status code before reading the stream
		}

		return new HttpStreamResponse(response, stream);
	}

	public void Dispose() {
		HttpClient.Dispose();
		HttpMessageHandler.Dispose();
	}

	# region System.MissingMethodException workarounds
	private static bool SetExpectContinueProperty(HttpClient httpClient, bool value) {
		try {
			// Get the DefaultRequestHeaders property
			PropertyInfo? defaultRequestHeadersProperty = httpClient.GetType().GetProperty(nameof(HttpClient.DefaultRequestHeaders), BindingFlags.Public | BindingFlags.Instance) ?? httpClient.GetType().GetProperty("DefaultRequestHeaders", BindingFlags.Public | BindingFlags.Instance);

			if (defaultRequestHeadersProperty == null) {
				throw new InvalidOperationException("HttpClient does not have DefaultRequestHeaders property.");
			}

			if (defaultRequestHeadersProperty.GetValue(httpClient) is not HttpRequestHeaders defaultRequestHeaders) {
				throw new InvalidOperationException("DefaultRequestHeaders is null.");
			}

			// Get the ExpectContinue property
			PropertyInfo? expectContinueProperty = defaultRequestHeaders.GetType().GetProperty(nameof(HttpRequestHeaders.ExpectContinue), BindingFlags.Public | BindingFlags.Instance) ?? defaultRequestHeaders.GetType().GetProperty("ExpectContinue", BindingFlags.Public | BindingFlags.Instance);

			if ((expectContinueProperty != null) && expectContinueProperty.CanWrite) {
				expectContinueProperty.SetValue(defaultRequestHeaders, value);

				return true;
			}
		}
		catch (Exception ex) {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericException(ex);
		}

		return false;
	}

	private static bool TrySetPropertyValue<T>(T targetObject, string propertyName, object value) where T : class {
		try {
			// Get the type of the target object
			Type targetType = targetObject.GetType();

			// Get the property information
			PropertyInfo? propertyInfo = targetType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

			if ((propertyInfo is not null) && propertyInfo.CanWrite) {
				// Set the property value
				propertyInfo.SetValue(targetObject, value);

				return true;
			}
		}
		catch (Exception ex) {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericException(ex);
		}

		return false;
	}

	private static void SetPropertyWithLogging<T>(T targetObject, string propertyName, object value, bool debugLogLevel = false) where T : class {
		try {
			if (TrySetPropertyValue(targetObject, propertyName, value)) {
				return;
			}
		}
		catch (Exception) {
			// ignored
		}

		string logMessage = $"Failed to set {targetObject.GetType().Name} property {propertyName} to {value}. Please report this issue to github.";

		if (debugLogLevel) {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericDebug(logMessage);
		}
		else {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericWarning(logMessage);
		}
	}
	#endregion
}

public sealed class HttpStreamResponse(HttpResponseMessage response, Stream? stream) : IAsyncDisposable {
	public HttpResponseMessage Response { get; } = response;
	public Stream Stream { get; } = stream ?? EmptyStreamLazy.Value;

	public bool HasValidStream => stream is not null && (!EmptyStreamLazy.IsValueCreated || !ReferenceEquals(EmptyStreamLazy.Value, Stream));

	public async Task<string> ReadAsStringAsync(CancellationToken cancellationToken) {
		using StreamReader reader = new(Stream); // assume the encoding is UTF8, cannot be specified as per issue #91

		return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
	}

	public HttpStatusCode StatusCode => Response.StatusCode;

	public async ValueTask DisposeAsync() {
		ValueTask task = HasValidStream ? Stream.DisposeAsync() : ValueTask.CompletedTask;
		Response.Dispose();
		await task.ConfigureAwait(false);
	}

	private static readonly Lazy<Stream> EmptyStreamLazy = new(static () => new MemoryStream([], false));
}
