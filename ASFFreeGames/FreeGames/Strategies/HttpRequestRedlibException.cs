using System;
using System.Net;
using Maxisoft.ASF.Redlib;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.FreeGames.Strategies;

public class HttpRequestRedlibException : RedlibException {
	public required HttpStatusCode? StatusCode { get; init; }
	public required Uri? Uri { get; init; }

	public HttpRequestRedlibException() { }
	public HttpRequestRedlibException(string message) : base(message) { }
	public HttpRequestRedlibException(string message, Exception inner) : base(message, inner) { }
}
