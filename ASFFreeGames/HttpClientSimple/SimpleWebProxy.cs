using System;
using System.Net;

namespace Maxisoft.ASF.HttpClientSimple;

/// <summary>
/// A simple IWebProxy implementation that avoids using System.Net.WebProxy
/// to prevent runtime version mismatch issues with WebProxy constructors.
/// </summary>
public sealed class SimpleWebProxy : IWebProxy {
	public Uri ProxyAddress { get; }
	public bool BypassOnLocal { get; }
	public ICredentials? Credentials { get; set; }

	public SimpleWebProxy(string address, bool bypassOnLocal = true) {
		ProxyAddress = new Uri(address);
		BypassOnLocal = bypassOnLocal;
	}

	public Uri? GetProxy(Uri destination) => ProxyAddress;

	public bool IsBypassed(Uri host) =>
		BypassOnLocal && (host.IsLoopback || host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
}
