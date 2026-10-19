using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace XFiler.YandexDisk.Client.Http;

/// <summary>
/// Collects one request-response pair for <see cref="ILogSaver"/>.
/// File bodies are never captured: only JSON API bodies, which the client buffers anyway.
/// </summary>
internal sealed class RequestLogger
{
    private readonly ILogSaver _saver;
    private readonly RequestLog _request;
    private readonly ResponseLog _response = new();
    private readonly long _startedAt = Stopwatch.GetTimestamp();

    private RequestLogger(ILogSaver saver, HttpRequestMessage request, byte[]? body)
    {
        _saver = saver;
        _request = new RequestLog
        {
            Uri = request.RequestUri?.ToString(),
            Headers = FormatHeaders(request),
            Body = body,
            StartedAt = DateTime.UtcNow
        };
    }

    public static RequestLogger? Start(ILogSaver? saver, HttpRequestMessage request, byte[]? body = null) =>
        saver is null ? null : new RequestLogger(saver, request, body);

    public void SetResponse(HttpResponseMessage response, byte[]? body)
    {
        _response.StatusCode = response.StatusCode;
        _response.Headers = response.ToString();
        _response.Body = body;
    }

    public void Success() => Save();

    public void Fail(Exception e)
    {
        _response.Exception = e.Message;
        Save();
    }

    private void Save()
    {
        _response.Duration = (long)Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds;
        _saver.SaveLog(_request, _response);
    }

    /// <summary>Like HttpRequestMessage.ToString, but without the OAuth token.</summary>
    private static string FormatHeaders(HttpRequestMessage request)
    {
        var builder = new StringBuilder()
            .Append("Method: ").Append(request.Method)
            .Append(", RequestUri: '").Append(request.RequestUri).Append('\'');

        Append(builder, request.Headers);

        if (request.Content is not null)
            Append(builder, request.Content.Headers);

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        foreach ((string name, IEnumerable<string> values) in headers)
        {
            builder.AppendLine().Append(name).Append(": ");
            builder.Append(name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                ? "<redacted>"
                : string.Join(", ", values));
        }
    }
}
