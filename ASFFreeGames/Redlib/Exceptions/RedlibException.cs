using System;

namespace Maxisoft.ASF.Redlib;

public abstract class RedlibException : Exception {
	protected RedlibException(string message) : base(message) { }

	protected RedlibException() { }

	protected RedlibException(string message, Exception innerException) : base(message, innerException) { }
}
