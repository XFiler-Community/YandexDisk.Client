using System;
using System.Net;

namespace XFiler.YandexDisk.Client.Http;

/// <summary>
/// Logger noticed on each request-response API operation.
/// </summary>
public interface ILogSaver
{
    /// <summary>
    /// Triggered on each request
    /// </summary>
    void SaveLog(RequestLog requestLog, ResponseLog responseLog);
}

/// <summary>
/// Describe output data of http request
/// </summary>
public class RequestLog
{
    /// <summary>
    /// Request Url
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// Sent headers to Yandex, the Authorization value is redacted
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    /// Sent request body to Yandex. Null for file uploads.
    /// </summary>
    public byte[]? Body { get; set; }

    /// <summary>
    /// Time of request started, UTC
    /// </summary>
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Describe input data of http request
/// </summary>
public class ResponseLog
{
    /// <summary>
    /// Response body from Yandex. Null for file downloads and failed connections.
    /// </summary>
    public byte[]? Body { get; set; }

    /// <summary>
    /// Response headers from Yandex
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    /// Exception text if it have been risen
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Http status code from Yandex
    /// </summary>
    public HttpStatusCode StatusCode { get; set; }

    /// <summary>
    /// Request-response time duration in milliseconds
    /// </summary>
    public long Duration { get; set; }
}
