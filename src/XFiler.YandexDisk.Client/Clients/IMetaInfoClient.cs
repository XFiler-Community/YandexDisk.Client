using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Clients;

/// <summary>
/// Files and folder metadata client
/// </summary>
public interface IMetaInfoClient
{
    /// <summary>
    /// Returns information about disk
    /// </summary>
    Task<Disk> GetDiskInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Return files or folder metadata
    /// </summary>
    Task<Resource> GetInfoAsync(ResourceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Return files or folder metadata in the trash
    /// </summary>
    Task<Resource> GetTrashInfoAsync(ResourceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flat file list on Disk
    /// </summary>
    Task<FilesResourceList> GetFilesInfoAsync(FilesResourceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Last uploaded file list on Disk
    /// </summary>
    Task<LastUploadedResourceList> GetLastUploadedInfoAsync(LastUploadedResourceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add or change custom properties of a resource. A null value removes the property.
    /// </summary>
    Task<Resource> AppendCustomPropertiesAsync(string path, IDictionary<string, string?> properties,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish file or folder: makes it available by a public link
    /// </summary>
    Task<Link> PublishAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpublish file or folder
    /// </summary>
    Task<Link> UnpublishAsync(string path, CancellationToken cancellationToken = default);
}
