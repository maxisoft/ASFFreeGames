using System;

namespace Maxisoft.ASF.Redlib;

public class RedlibDisabledException : RedlibException {
	public RedlibDisabledException(string message) : base(message) { }

	public RedlibDisabledException() { }

	public RedlibDisabledException(string message, Exception innerException) : base(message, innerException) { }
}
