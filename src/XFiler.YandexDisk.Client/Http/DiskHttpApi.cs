using System;
using System.Net.Http;
using System.Threading;
using XFiler.YandexDisk.Client.Clients;
using XFiler.YandexDisk.Client.Http.Clients;

namespace XFiler.YandexDisk.Client.Http;

/// <remarks>
/// This object is thread safe. Keep one instance per token and reuse it between requests.
/// </remarks>
public sealed class DiskHttpApi : IDiskApi
{
    /// <summary>Default base url to Yandex Disk API</summary>
    public const string YandexDiskApiBaseUrl = "https://cloud-api.yandex.net/v1/disk/";

    private readonly ApiContext _context;

    /// <summary>
    /// Create a client with its own <see cref="HttpClient"/>, disposed together with this object.
    /// </summary>
    /// <param name="oauthToken">
    /// OAuth token for the API, see <see href="https://yandex.ru/dev/disk/rest/"/>
    /// </param>
    /// <param name="logSaver">Receives every request-response pair, without file bodies.</param>
    public DiskHttpApi(string oauthToken, ILogSaver? logSaver = null)
        : this(
            // No HttpClient timeout: transfers of large files are bounded by the caller's token,
            // API calls by ApiTimeout.
            new RealHttpClientWrapper(new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, ownsHttpClient: true),
            YandexDiskApiBaseUrl, logSaver, RequireToken(oauthToken))
    {
    }

    /// <summary>
    /// Create a client on top of an existing <see cref="HttpClient"/>, for example one from IHttpClientFactory.
    /// The token is sent per request, the client's default headers are not changed, and it is not disposed.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpClient.Timeout"/> also limits file transfers: set it to
    /// <see cref="Timeout.InfiniteTimeSpan"/> to upload and download large files.
    /// </remarks>
    public DiskHttpApi(HttpClient httpClient, string oauthToken, ILogSaver? logSaver = null)
        : this(
            new RealHttpClientWrapper(httpClient ?? throw new ArgumentNullException(nameof(httpClient)), ownsHttpClient: false),
            YandexDiskApiBaseUrl, logSaver, RequireToken(oauthToken))
    {
    }

    /// <summary>
    /// Create a client on a custom transport, mostly for tests. Sends no OAuth header.
    /// The transport is disposed together with this object.
    /// </summary>
    public DiskHttpApi(string baseUrl, ILogSaver? logSaver, IHttpClient httpClient)
        : this(httpClient ?? throw new ArgumentNullException(nameof(httpClient)), baseUrl, logSaver, oauthToken: null)
    {
    }

    private DiskHttpApi(IHttpClient httpClient, string baseUrl, ILogSaver? logSaver, string? oauthToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);

        var baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute);

        _context = new ApiContext(httpClient, baseUri, logSaver, oauthToken);

        Files = new FilesClient(_context);
        MetaInfo = new MetaInfoClient(_context);
        Commands = new CommandsClient(_context);
    }

    /// <inheritdoc />
    public IFilesClient Files { get; }

    /// <inheritdoc />
    public IMetaInfoClient MetaInfo { get; }

    /// <inheritdoc />
    public ICommandsClient Commands { get; }

    /// <summary>
    /// Timeout of metadata and command calls, 100 s by default.
    /// Uploads and downloads are not limited by it.
    /// </summary>
    public TimeSpan ApiTimeout
    {
        get => _context.ApiTimeout;
        set
        {
            if (value <= TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Timeout must be positive or infinite.");

            _context.ApiTimeout = value;
        }
    }

    /// <summary>
    /// Dispose the owned transport
    /// </summary>
    public void Dispose() => _context.HttpClient.Dispose();

    private static string RequireToken(string oauthToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oauthToken);

        return oauthToken;
    }
}
