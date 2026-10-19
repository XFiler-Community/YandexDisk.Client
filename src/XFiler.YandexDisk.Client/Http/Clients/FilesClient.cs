using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Clients;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Http.Clients;

internal sealed class FilesClient(ApiContext context) : DiskClientBase(context), IFilesClient
{
    public Task<Link> GetUploadLinkAsync(string path, bool overwrite, CancellationToken cancellationToken = default) =>
        SendApiRequiredAsync(HttpMethod.Get,
            ApiUrl("resources/upload", new Query().Add("path", path).Add("overwrite", overwrite)),
            YandexDiskJsonContext.Default.Link, cancellationToken);

    public Task UploadAsync(Link link, Stream file, CancellationToken cancellationToken = default) =>
        UploadCoreAsync(link, new UploadContent(file, length: null, bytesSent: null), cancellationToken);

    public Task UploadWithProgressAsync(Link link, Stream file, long fileSize, Action<double>? progressCallback,
        CancellationToken cancellationToken = default)
    {
        Action<long>? bytesSent = progressCallback is null || fileSize <= 0
            ? null
            : sent => progressCallback((double)sent / fileSize);

        return UploadCoreAsync(link, new UploadContent(file, fileSize > 0 ? fileSize : null, bytesSent), cancellationToken);
    }

    public Task<Link> GetDownloadLinkAsync(string path, CancellationToken cancellationToken = default) =>
        SendApiRequiredAsync(HttpMethod.Get, ApiUrl("resources/download", new Query().Add("path", path)),
            YandexDiskJsonContext.Default.Link, cancellationToken);

    public async Task<Stream> DownloadAsync(Link link, CancellationToken cancellationToken = default) =>
        (await DownloadFastAsync(link, cancellationToken).ConfigureAwait(false)).Stream;

    public async Task<(Stream Stream, long Length)> DownloadFastAsync(Link link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        using HttpRequestMessage request = CreateRequest(link);

        // Headers only: the body is handed to the caller as a stream instead of being buffered.
        HttpResponseMessage response = await SendTransferAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        try
        {
            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            return (stream, response.Content.Headers.ContentLength ?? 0);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private async Task UploadCoreAsync(Link link, UploadContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        using HttpRequestMessage request = CreateRequest(link);
        request.Content = content;

        using HttpResponseMessage response = await SendTransferAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
    }
}
