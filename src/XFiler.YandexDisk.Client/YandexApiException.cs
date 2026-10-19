using System;
using System.Net;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client;

/// <summary>
/// The API answered with an error status.
/// </summary>
public class YandexApiException : Exception
{
    /// <summary>
    /// HTTP status of the failed response
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// HTTP reason phrase of the failed response
    /// </summary>
    public string? ReasonPhrase { get; }

    /// <summary>
    /// Error code and description from the response body, when it had one
    /// </summary>
    public ErrorDescription? Error { get; }

    public YandexApiException(HttpStatusCode statusCode, string? reasonPhrase, ErrorDescription? error,
        Exception? innerException = null)
        : base(error?.Description ?? reasonPhrase ?? $"HTTP {(int)statusCode}", innerException)
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Error = error;
    }

    public override string ToString() => $"StatusCode: {StatusCode}, {ReasonPhrase}. {base.ToString()}";
}

/// <summary>
/// The OAuth token is missing, invalid or expired (HTTP 401).
/// </summary>
public class NotAuthorizedException(HttpStatusCode statusCode, string? reasonPhrase, ErrorDescription? error)
    : YandexApiException(statusCode, reasonPhrase, error);

/// <summary>
/// An asynchronous operation (copy, move, delete, trash) ended with the "failure" status.
/// </summary>
public class YandexOperationFailedException(Link operation)
    : YandexApiException(HttpStatusCode.OK, reasonPhrase: null,
        new ErrorDescription { Error = "OperationFailed", Description = $"Operation failed: {operation.Href}" })
{
    /// <summary>
    /// Link to the operation status
    /// </summary>
    public Link Operation { get; } = operation;
}
