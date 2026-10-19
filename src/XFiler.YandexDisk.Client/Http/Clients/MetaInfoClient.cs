using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Clients;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Http.Clients;

internal sealed class MetaInfoClient(ApiContext context) : DiskClientBase(context), IMetaInfoClient
{
    private static readonly HttpMethod Patch = new("PATCH");

    public Task<Disk> GetDiskInfoAsync(CancellationToken cancellationToken = default) =>
        SendApiRequiredAsync(HttpMethod.Get, ApiUrl(""), YandexDiskJsonContext.Default.Disk, cancellationToken);

    public Task<Resource> GetInfoAsync(ResourceRequest request, CancellationToken cancellationToken = default) =>
        GetResourceAsync("resources", request, cancellationToken);

    public Task<Resource> GetTrashInfoAsync(ResourceRequest request, CancellationToken cancellationToken = default) =>
        GetResourceAsync("trash/resources", request, cancellationToken);

    public Task<FilesResourceList> GetFilesInfoAsync(FilesResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Query query = new Query()
            .Add("media_type", MediaTypes(request.MediaType))
            .Add("limit", request.Limit)
            .Add("offset", request.Offset);

        return SendApiRequiredAsync(HttpMethod.Get, ApiUrl("resources/files", query),
            YandexDiskJsonContext.Default.FilesResourceList, cancellationToken);
    }

    public Task<LastUploadedResourceList> GetLastUploadedInfoAsync(LastUploadedResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Query query = new Query()
            .Add("media_type", MediaTypes(request.MediaType))
            .Add("limit", request.Limit);

        return SendApiRequiredAsync(HttpMethod.Get, ApiUrl("resources/last-uploaded", query),
            YandexDiskJsonContext.Default.LastUploadedResourceList, cancellationToken);
    }

    public Task<Resource> AppendCustomPropertiesAsync(string path, IDictionary<string, string?> properties,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(properties);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(
            new CustomPropertiesRequest { CustomProperties = properties },
            YandexDiskJsonContext.Default.CustomPropertiesRequest);

        return SendApiRequiredAsync(Patch, ApiUrl("resources", new Query().Add("path", path)),
            YandexDiskJsonContext.Default.Resource, cancellationToken, body);
    }

    public Task<Link> PublishAsync(string path, CancellationToken cancellationToken = default) =>
        SendApiRequiredAsync(HttpMethod.Put, ApiUrl("resources/publish", new Query().Add("path", path)),
            YandexDiskJsonContext.Default.Link, cancellationToken);

    public Task<Link> UnpublishAsync(string path, CancellationToken cancellationToken = default) =>
        SendApiRequiredAsync(HttpMethod.Put, ApiUrl("resources/unpublish", new Query().Add("path", path)),
            YandexDiskJsonContext.Default.Link, cancellationToken);

    private Task<Resource> GetResourceAsync(string relativeUrl, ResourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Query query = new Query()
            .Add("path", request.Path)
            .Add("limit", request.Limit)
            .Add("offset", request.Offset)
            .Add("sort", request.Sort);

        return SendApiRequiredAsync(HttpMethod.Get, ApiUrl(relativeUrl, query),
            YandexDiskJsonContext.Default.Resource, cancellationToken);
    }

    private static string? MediaTypes(MediaType[]? mediaTypes)
    {
        if (mediaTypes is null || mediaTypes.Length == 0)
            return null;

        var builder = new StringBuilder();

        foreach (MediaType mediaType in mediaTypes)
        {
            if (builder.Length > 0)
                builder.Append(',');

            builder.Append(mediaType.ToApiValue());
        }

        return builder.ToString();
    }
}
