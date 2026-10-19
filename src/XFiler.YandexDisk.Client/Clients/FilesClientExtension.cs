using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Clients;

/// <summary>
/// Extended helpers from uploading and downloading files
/// </summary>
public static class FilesClientExtension
{
    private const int FileBufferSize = 81920;

    /// <summary>
    /// Upload stream data to Yandex Disk. The stream is not disposed.
    /// </summary>
    public static async Task UploadFileAsync(this IFilesClient client, string path, bool overwrite, Stream file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(file);

        Link link = await client.GetUploadLinkAsync(path, overwrite, cancellationToken).ConfigureAwait(false);

        await client.UploadAsync(link, file, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Upload file from local disk to Yandex Disk
    /// </summary>
    public static async Task UploadFileAsync(this IFilesClient client, string path, bool overwrite, string localFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(localFile);

        // Open first: a missing local file fails before an upload link is requested.
        await using var file = new FileStream(localFile, FileMode.Open, FileAccess.Read, FileShare.Read,
            FileBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        Link link = await client.GetUploadLinkAsync(path, overwrite, cancellationToken).ConfigureAwait(false);

        await client.UploadAsync(link, file, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Open file from Yandex Disk as a network stream. Dispose it to release the connection.
    /// </summary>
    public static async Task<Stream> DownloadFileAsync(this IFilesClient client, string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Link link = await client.GetDownloadLinkAsync(path, cancellationToken).ConfigureAwait(false);

        return await client.DownloadAsync(link, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Download file from Yandex Disk to a local file, replacing it if it exists
    /// </summary>
    public static async Task DownloadFileAsync(this IFilesClient client, string path, string localFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(localFile);

        await using Stream data = await client.DownloadFileAsync(path, cancellationToken).ConfigureAwait(false);

        await using var file = new FileStream(localFile, FileMode.Create, FileAccess.Write, FileShare.None,
            FileBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        await data.CopyToAsync(file, FileBufferSize, cancellationToken).ConfigureAwait(false);
    }
}
