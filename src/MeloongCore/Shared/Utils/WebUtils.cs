namespace MeloongCore;

/// <summary>
/// Exception thrown when the status code of an HTTP request does not indicate success.
/// </summary>
public class HttpRequestCodeException(string message, HttpStatusCode statusCode, string? response) : HttpRequestException(message) {
    /// <summary>
    /// The status code of the HTTP request.
    /// </summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
    /// <summary>
    /// The response returned by the remote server.
    /// </summary>
    public string? Response { get; } = response;
}
