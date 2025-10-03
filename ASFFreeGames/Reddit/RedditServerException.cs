using System;
using System.Net;

namespace Maxisoft.ASF.Reddit;

public class RedditServerException : Exception {
	// A property to store the status code of the response
	public HttpStatusCode StatusCode { get; }

	// A constructor that takes a message and a status code as parameters
	public RedditServerException(string message, HttpStatusCode statusCode)
		: base(message) => StatusCode = statusCode;

	public RedditServerException() { }

	public RedditServerException(string message)
		: base(message) { }

	public RedditServerException(string message, Exception innerException)
		: base(message, innerException) { }
}
