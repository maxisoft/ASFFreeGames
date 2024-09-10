using System;

namespace Maxisoft.ASF.Redlib;

public class RedlibOutDatedListException : RedlibException {
	public RedlibOutDatedListException(string message) : base(message) { }

	public RedlibOutDatedListException() { }

	public RedlibOutDatedListException(string message, Exception innerException) : base(message, innerException) { }
}
