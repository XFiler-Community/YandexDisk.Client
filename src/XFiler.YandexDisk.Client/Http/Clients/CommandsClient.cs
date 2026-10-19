using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Clients;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Http.Clients;

internal sealed class CommandsClient(ApiContext context) : DiskClientBase(context), ICommandsClient
{
    public Task<Link> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        SendApiRequiredAsync(HttpMethod.Put, ApiUrl("resources", new Query().Add("path", path)),
            YandexDiskJsonContext.Default.Link, cancellationToken);

    public Task<Link> CopyAsync(CopyFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Query query = new Query()
            .Add("from", request.From)
            .Add("path", request.Path)
            .Add("overwrite", request.Overwrite);

        return SendApiRequiredAsync(HttpMethod.Post, ApiUrl("resources/copy", query),
            YandexDiskJsonContext.Default.Link, cancellationToken);
    }

    public Task<Link> MoveAsync(MoveFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Query query = new Query()
            .Add("from", request.From)
            .Add("path", request.Path)
            .Add("overwrite", request.Overwrite);

        return SendApiRequiredAsync(HttpMethod.Post, ApiUrl("resources/move", query),
            YandexDiskJsonContext.Default.Link, cancellationToken);
    }

    public Task<Link?> DeleteAsync(DeleteFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Query query = new Query()
            .Add("path", request.Path)
            .Add("permanently", request.Permanently);

        return SendApiAsync(HttpMethod.Delete, ApiUrl("resources", query),
            YandexDiskJsonContext.Default.Link, cancellationToken);
    }

    public Task<Link?> EmptyTrashAsync(string? path = null, CancellationToken cancellationToken = default) =>
        SendApiAsync(HttpMethod.Delete, ApiUrl("trash/resources", new Query().Add("path", path)),
            YandexDiskJsonContext.Default.Link, cancellationToken);

    public Task<Link> RestoreFromTrashAsync(RestoreFromTrashRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Query query = new Query()
            .Add("path", request.Path)
            .Add("overwrite", request.Overwrite)
            .Add("name", request.Name);

        return SendApiRequiredAsync(HttpMethod.Put, ApiUrl("trash/resources", query),
            YandexDiskJsonContext.Default.Link, cancellationToken);
    }

    public Task<Operation> GetOperationStatusAsync(Link link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        return SendApiRequiredAsync(new HttpMethod(link.Method), new Uri(link.Href),
            YandexDiskJsonContext.Default.Operation, cancellationToken);
    }
}
