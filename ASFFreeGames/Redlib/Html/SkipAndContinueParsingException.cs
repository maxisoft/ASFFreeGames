using System;

namespace Maxisoft.ASF.Redlib.Html;

public class SkipAndContinueParsingException : Exception {
	public int StartIndex { get; init; }

	public SkipAndContinueParsingException(string message, Exception innerException) : base(message, innerException) { }

	public SkipAndContinueParsingException() { }

	public SkipAndContinueParsingException(string message) : base(message) { }
}
