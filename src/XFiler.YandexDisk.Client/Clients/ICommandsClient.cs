using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Clients;

/// <summary>
/// Disk file operations. Copy, move, delete and trash calls on large folders may answer with
/// 202 Accepted and a link to an asynchronous operation; the *AndWaitAsync helpers in
/// <see cref="CommandsClientExtensions"/> wait for it.
/// </summary>
public interface ICommandsClient
{
    /// <summary>
    /// Create folder on Disk
    /// </summary>
    Task<Link> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copy file or folder on Disk from one path to another
    /// </summary>
    Task<Link> CopyAsync(CopyFileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move file or folder on Disk from one path to another
    /// </summary>
    Task<Link> MoveAsync(MoveFileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete file or folder on Disk. Null when it was deleted at once (204 No Content).
    /// </summary>
    Task<Link?> DeleteAsync(DeleteFileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete files in trash. Null when it was done at once (204 No Content).
    /// </summary>
    Task<Link?> EmptyTrashAsync(string? path = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restore file or folder from trash
    /// </summary>
    Task<Link> RestoreFromTrashAsync(RestoreFromTrashRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Return status of operation
    /// </summary>
    Task<Operation> GetOperationStatusAsync(Link link, CancellationToken cancellationToken = default);
}
