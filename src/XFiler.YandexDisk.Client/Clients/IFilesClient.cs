using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Clients;

/// <summary>
/// Files operation client
/// </summary>
public interface IFilesClient
{
    /// <summary>
    /// Return link for file upload
    /// </summary>
    /// <param name="path">Path on Disk for uploading file</param>
    /// <param name="overwrite">If file exists it will be overwritten</param>
    /// <param name="cancellationToken"></param>
    Task<Link> GetUploadLinkAsync(string path, bool overwrite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload <paramref name="file"/> from its current position to a link from <see cref="GetUploadLinkAsync"/>.
    /// The stream is not disposed.
    /// </summary>
    Task UploadAsync(Link link, Stream file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload like <see cref="UploadAsync"/>, reporting the sent fraction (0.0 – 1.0) of <paramref name="fileSize"/>.
    /// </summary>
    Task UploadWithProgressAsync(Link link, Stream file, long fileSize, Action<double>? progressCallback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ask Yandex to download the file at <paramref name="url"/> into <paramref name="path"/> by itself
    /// (<c>POST resources/upload?url=</c>). The answer is 202 Accepted with the link of the operation: wait for it with
    /// <see cref="CommandsClientExtensions.WaitForOperationAsync"/>. The operation has no progress, and Yandex does not
    /// retry a failed download. A path that already exists is refused (409): there is no overwrite.
    /// </summary>
    /// <param name="path">Path on Disk for the downloaded file</param>
    /// <param name="url">Address Yandex downloads the file from; it must not need any credentials of yours</param>
    /// <param name="disableRedirects">Refuse redirects of <paramref name="url"/> instead of following them</param>
    /// <param name="cancellationToken">Cancels the request only: an accepted operation goes on at Yandex</param>
    /// <remarks>The default implementation, for clients written before this existed, is not supported.</remarks>
    Task<Link> UploadFromUrlAsync(string path, string url, bool disableRedirects = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This client does not upload from a URL.");

    /// <summary>
    /// Return link for file download
    /// </summary>
    /// <param name="path">Path to downloading file on Disk</param>
    /// <param name="cancellationToken"></param>
    Task<Link> GetDownloadLinkAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Open the file from a link from <see cref="GetDownloadLinkAsync"/> as a network stream.
    /// The file is not buffered in memory; dispose the stream to release the connection.
    /// </summary>
    Task<Stream> DownloadAsync(Link link, CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="DownloadAsync"/>, also returning the file length from Content-Length (0 if unknown).
    /// </summary>
    Task<(Stream Stream, long Length)> DownloadFastAsync(Link link, CancellationToken cancellationToken = default);

    /// <summary>
    /// Open <paramref name="count"/> bytes from <paramref name="offset"/> of the file behind a link from
    /// <see cref="GetDownloadLinkAsync"/> (an HTTP Range request). A link serves any number of ranges, so a large file
    /// can be read as several ranges at once: Yandex limits the speed of each connection, not of the account.
    /// <c>IsPartial</c> is false when the server ignored the range and sent the whole file instead;
    /// <c>TotalLength</c> is the file length from Content-Range, null when the server did not say.
    /// </summary>
    /// <remarks>The default implementation, for clients written before ranges existed, sends the whole file.</remarks>
    async Task<(Stream Stream, bool IsPartial, long? TotalLength)> DownloadRangeAsync(Link link, long offset, long count,
        CancellationToken cancellationToken = default)
    {
        (Stream stream, long length) = await DownloadFastAsync(link, cancellationToken).ConfigureAwait(false);

        return (stream, false, length > 0 ? length : null);
    }
}
