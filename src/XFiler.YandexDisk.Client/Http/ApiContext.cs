using System;
using System.Net.Http.Headers;

namespace XFiler.YandexDisk.Client.Http;

internal sealed class ApiContext(IHttpClient httpClient, Uri baseUrl, ILogSaver? logSaver, string? oauthToken)
{
    public IHttpClient HttpClient { get; } = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <summary>Always ends with '/', so relative paths resolve under it.</summary>
    public Uri BaseUrl { get; } = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));

    public ILogSaver? LogSaver { get; } = logSaver;

    /// <summary>Set on every request instead of on the HttpClient, so a shared HttpClient stays untouched.</summary>
    public AuthenticationHeaderValue? Authorization { get; } =
        oauthToken is null ? null : new AuthenticationHeaderValue("OAuth", oauthToken);

    /// <summary>Timeout of JSON API calls. File transfers are bounded only by the caller's token.</summary>
    public TimeSpan ApiTimeout { get; set; } = TimeSpan.FromSeconds(100);
}
