using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Http;

namespace YandexDisk.Client.Tests;

/// <summary>
/// Answers requests from a queue of scripted responses and records what was sent.
/// </summary>
internal sealed class FakeHttpClient : IHttpClient
{
    public const string BaseUrl = "http://ya.ru/api/";

    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new();

    public List<(string Method, string Url, HttpCompletionOption CompletionOption)> Requests { get; } = [];

    public FakeHttpClient Respond(HttpStatusCode statusCode, string? body = null, string mediaType = "application/json")
    {
        _responses.Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body ?? "", Encoding.UTF8, mediaType)
        }));

        return this;
    }

    public FakeHttpClient Respond(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _responses.Enqueue(handler);

        return this;
    }

    public FakeHttpClient RespondOperation(string status) =>
        Respond(HttpStatusCode.OK, $$"""{ "status": "{{status}}" }""");

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        // AbsoluteUri is what goes on the wire; Uri.ToString() shows the query unescaped.
        Requests.Add((request.Method.Method, request.RequestUri!.AbsoluteUri, completionOption));

        if (_responses.Count == 0)
            throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");

        return _responses.Dequeue()(request, cancellationToken);
    }

    public DiskHttpApi CreateApi(ILogSaver? logSaver = null) => new(BaseUrl, logSaver, this);

    public void Dispose()
    {
    }
}
