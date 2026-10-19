using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Http;

internal abstract class DiskClientBase(ApiContext context)
{
    private static readonly ProductInfoHeaderValue UserAgent = new(
        "XFiler.YandexDisk.Client",
        typeof(DiskClientBase).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    private static readonly MediaTypeHeaderValue JsonMediaType = new("application/json") { CharSet = "utf-8" };

    private protected ApiContext Context { get; } = context;

    private protected Uri ApiUrl(string relativeUrl, Query? query = null) =>
        new(Context.BaseUrl, query is null ? relativeUrl : relativeUrl + query);

    /// <summary>
    /// Calls a JSON endpoint. Returns null when the response has no body (204 No Content).
    /// </summary>
    private protected async Task<T?> SendApiAsync<T>(HttpMethod method, Uri url, JsonTypeInfo<T> responseType,
        CancellationToken cancellationToken, byte[]? jsonBody = null)
        where T : ProtocolObjectResponse
    {
        using HttpRequestMessage request = CreateRequest(method, url);

        if (jsonBody is not null)
        {
            request.Content = new ByteArrayContent(jsonBody);
            request.Content.Headers.ContentType = JsonMediaType;
        }

        RequestLogger? log = RequestLogger.Start(Context.LogSaver, request, jsonBody);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Context.ApiTimeout);

        try
        {
            using HttpResponseMessage response = await Context.HttpClient
                .SendAsync(request, timeout.Token, HttpCompletionOption.ResponseContentRead)
                .ConfigureAwait(false);

            // API bodies are small: one buffer, deserialized as UTF-8 with no string in between.
            byte[] body = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);

            log?.SetResponse(response, body);

            if (!response.IsSuccessStatusCode)
                throw CreateException(response, body);

            T? result = body.Length == 0 ? null : JsonSerializer.Deserialize(body, responseType);

            if (result is not null)
                result.HttpStatusCode = response.StatusCode;

            log?.Success();

            return result;
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutException = new TimeoutException(
                $"Yandex Disk API did not respond within {Context.ApiTimeout.TotalSeconds:0} s: {method} {url}", e);

            log?.Fail(timeoutException);

            throw timeoutException;
        }
        catch (Exception e)
        {
            log?.Fail(e);

            throw;
        }
    }

    /// <summary>
    /// Calls a JSON endpoint that always answers with a body.
    /// </summary>
    private protected async Task<T> SendApiRequiredAsync<T>(HttpMethod method, Uri url, JsonTypeInfo<T> responseType,
        CancellationToken cancellationToken, byte[]? jsonBody = null)
        where T : ProtocolObjectResponse
    {
        return await SendApiAsync(method, url, responseType, cancellationToken, jsonBody).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Yandex Disk API returned an empty response: {method} {url}");
    }

    /// <summary>
    /// Sends a file transfer request. The caller owns the returned response.
    /// Bodies are not buffered or logged.
    /// </summary>
    private protected async Task<HttpResponseMessage> SendTransferAsync(HttpRequestMessage request,
        HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        RequestLogger? log = RequestLogger.Start(Context.LogSaver, request);

        try
        {
            HttpResponseMessage response = await Context.HttpClient
                .SendAsync(request, cancellationToken, completionOption)
                .ConfigureAwait(false);

            log?.SetResponse(response, body: null);

            if (!response.IsSuccessStatusCode)
            {
                using (response)
                {
                    byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

                    throw CreateException(response, body);
                }
            }

            log?.Success();

            return response;
        }
        catch (Exception e)
        {
            log?.Fail(e);

            throw;
        }
    }

    private protected HttpRequestMessage CreateRequest(HttpMethod method, Uri url)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.Authorization = Context.Authorization;
        request.Headers.UserAgent.Add(UserAgent);

        return request;
    }

    private protected HttpRequestMessage CreateRequest(Link link) =>
        CreateRequest(new HttpMethod(link.Method), new Uri(link.Href));

    private static YandexApiException CreateException(HttpResponseMessage response, byte[] body)
    {
        ErrorDescription? error = TryReadError(body);

        return response.StatusCode == HttpStatusCode.Unauthorized
            ? new NotAuthorizedException(response.StatusCode, response.ReasonPhrase, error)
            : new YandexApiException(response.StatusCode, response.ReasonPhrase, error);
    }

    /// <summary>Error bodies may be HTML from a proxy or empty; the status code still matters then.</summary>
    private static ErrorDescription? TryReadError(byte[] body)
    {
        if (body.Length == 0)
            return null;

        try
        {
            return JsonSerializer.Deserialize(body, YandexDiskJsonContext.Default.ErrorDescription);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
