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
}
