# XFiler.YandexDisk.Client

[![CI](https://github.com/XFiler-Community/YandexDisk.Client/actions/workflows/ci.yml/badge.svg)](https://github.com/XFiler-Community/YandexDisk.Client/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/XFiler.YandexDisk.Client.svg)](https://www.nuget.org/packages/XFiler.YandexDisk.Client)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A .NET client for the [Yandex Disk REST API](https://yandex.ru/dev/disk/rest/). It is built for Native AOT and trimming: JSON goes through `System.Text.Json` source generation, there is no reflection, and the package depends only on the BCL. It powers Yandex Disk support in [X-Filer](https://x-filer.com) through [FileVault](https://github.com/XFiler-Community/FileVault).

- Files: upload with progress, downloads streamed straight from the network (never buffered in memory)
- Metadata: disk quota, file and folder info with paging and sorting, flat file list, recent uploads, custom properties
- Commands: create folder, copy, move, delete, trash and restore, with helpers that wait for async operations to finish and throw if they fail
- Publishing: publish and unpublish files and folders
- `IsAotCompatible` with zero trim/AOT warnings, nullable annotations, `CancellationToken` on every call
- Works on your own `HttpClient` (for example from `IHttpClientFactory`) without touching its default headers

## Installation

```shell
dotnet add package XFiler.YandexDisk.Client
```

Targets `net8.0` (works on any newer .NET).

## Quick start

You need an OAuth token for a Yandex app with Disk access. See [Yandex OAuth](https://yandex.ru/dev/id/doc/en/) and the [Disk API quick start](https://yandex.ru/dev/disk/rest/).

```csharp
using XFiler.YandexDisk.Client;
using XFiler.YandexDisk.Client.Clients;
using XFiler.YandexDisk.Client.Http;
using XFiler.YandexDisk.Client.Protocol;

// One instance per token, reuse it: it owns an HttpClient.
using IDiskApi disk = new DiskHttpApi(oauthToken);

Disk info = await disk.MetaInfo.GetDiskInfoAsync();
Console.WriteLine($"{info.UsedSpace} of {info.TotalSpace} bytes used");
```

`IDiskApi` has three clients: `Files` (transfer), `MetaInfo` (read metadata), `Commands` (change things).

To reuse an existing `HttpClient`, pass it in. The token goes on each request, so the client's default headers stay as they are, and it is not disposed:

```csharp
var disk = new DiskHttpApi(httpClientFactory.CreateClient("yandex"), oauthToken);
```

Its `HttpClient.Timeout` also limits uploads and downloads, so set it to `Timeout.InfiniteTimeSpan` for large files. With the first constructor transfers have no timeout at all (use the `CancellationToken`), and metadata and command calls time out after `ApiTimeout` (100 s by default) with a `TimeoutException`.

## Listing a folder

```csharp
Resource folder = await disk.MetaInfo.GetInfoAsync(new ResourceRequest
{
    Path = "/Documents",
    Limit = 100,
    Offset = 0,
    Sort = "-modified"
});

foreach (Resource item in folder.Embedded.Items)
    Console.WriteLine($"{(item.Type == ResourceType.Dir ? "dir " : "file")} {item.Name} {item.Size}");

// folder.Embedded.Total is the item count, page through it with Offset.
```

## Uploading and downloading

```csharp
// Upload a local file (overwrite if it exists)
await disk.Files.UploadFileAsync("/Documents/report.pdf", overwrite: true, "report.pdf", CancellationToken.None);

// Upload a stream with progress (0.0 – 1.0)
await using var stream = File.OpenRead("video.mp4");
Link uploadLink = await disk.Files.GetUploadLinkAsync("/Video/video.mp4", overwrite: false);
await disk.Files.UploadWithProgressAsync(uploadLink, stream, stream.Length,
    progress => Console.WriteLine($"{progress:P0}"));

// Download to a local file
await disk.Files.DownloadFileAsync("/Documents/report.pdf", "report-copy.pdf");

// Stream a download; dispose the stream to release the connection
Link downloadLink = await disk.Files.GetDownloadLinkAsync("/Video/video.mp4");
var (content, length) = await disk.Files.DownloadFastAsync(downloadLink);
await using (content)
{
    // read content
}
```

## Files and folders

Copy, move, delete and trash operations on large folders run asynchronously on Yandex's side. The `*AndWaitAsync` helpers poll the operation until it finishes: first after 250 ms, then with a doubling interval up to `pollInterval` (3 s by default). A failed operation throws `YandexOperationFailedException`, cancellation throws `OperationCanceledException`.

```csharp
await disk.Commands.CreateDirectoryAsync("/Projects/2026");

await disk.Commands.CopyAndWaitAsync(new CopyFileRequest { From = "/Projects/2025", Path = "/Archive/2025" });
await disk.Commands.MoveAndWaitAsync(new MoveFileRequest { From = "/Inbox/a.txt", Path = "/Docs/a.txt", Overwrite = true });

// To the trash, or Permanently = true to skip it
await disk.Commands.DeleteAndWaitAsync(new DeleteFileRequest { Path = "/Docs/a.txt" });

await disk.Commands.RestoreFromTrashAndWaitAsync(new RestoreFromTrashRequest { Path = "trash:/a.txt" });
await disk.Commands.EmptyTrashAndWaitAsync();
```

Without the helpers, `CopyAsync`, `MoveAsync` and the rest return a `Link`. When its `HttpStatusCode` is `202 Accepted` it points to the operation: wait for it with `WaitForOperationAsync`, or check it yourself with `GetOperationStatusAsync`.

## Publishing

```csharp
await disk.MetaInfo.PublishAsync("/Shared/photos");
Resource shared = await disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = "/Shared/photos" });
Console.WriteLine(shared.PublicUrl);

await disk.MetaInfo.UnpublishAsync("/Shared/photos");
```

## Errors

Failed requests throw `YandexApiException` with `StatusCode`, `ReasonPhrase` and the API's `ErrorDescription` (null when the body was not JSON, for example a proxy's 502 page). An invalid or expired token (401) throws its subclass `NotAuthorizedException`, a failed asynchronous operation throws `YandexOperationFailedException`.

```csharp
try
{
    await disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = "/missing" });
}
catch (NotAuthorizedException)
{
    // refresh the token
}
catch (YandexApiException e) when (e.StatusCode == HttpStatusCode.NotFound)
{
    // no such resource
}
```

## Logging and testing

`DiskHttpApi(oauthToken, logSaver)` accepts an `ILogSaver` that receives every request and response for diagnostics. The OAuth token is redacted, and file bodies are never captured. To test without the network, pass your own `IHttpClient` to `DiskHttpApi(baseUrl, logSaver, httpClient)`.

## Migrating

**From `Egorozh.YandexDisk.Client` 8.x.** 9.0.0 renames the package and namespace and fixes the API along the way.

```diff
- <PackageReference Include="Egorozh.YandexDisk.Client" Version="8.2.0" />
+ <PackageReference Include="XFiler.YandexDisk.Client" Version="9.0.0" />
```

Replace `Egorozh.YandexDisk.Client` with `XFiler.YandexDisk.Client` in `using` directives, then:

| 8.x | 9.0 |
|---|---|
| `CreateDictionaryAsync` | `CreateDirectoryAsync` |
| `GetOperationStatus` | `GetOperationStatusAsync` |
| `EmptyTrashAndWaitAsyncAsync`, `RestoreFromTrashAndWaitAsyncAsync` | `EmptyTrashAndWaitAsync`, `RestoreFromTrashAndWaitAsync` |
| `PublishFolderAsync`, `UnpublishFolderAsync` | `PublishAsync`, `UnpublishAsync` |
| `AppendCustomProperties` | `AppendCustomPropertiesAsync`, values are `string?` (null removes a property) |
| `EmptyTrashAsync` returns `Link` | returns `Link?`: null when done at once |
| `CommandsClientExtensions.DefaultPullPeriod`, `pullPeriod` | `pollInterval` argument |
| `HttpClientFactory`, `ProgressMessageHandler`, `HttpProgressEventArgs`, `AboutInfo` | removed |

Behavior changes:

- `*AndWaitAsync` no longer block a thread, throw `YandexOperationFailedException` when the operation fails and `OperationCanceledException` when cancelled (they used to return as if it succeeded).
- `DownloadAsync` and `DownloadFileAsync` stream the file instead of reading it into memory. `DownloadFileAsync(path, localFile)` overwrites an existing file.
- Uploads with progress use the same `HttpClient` and are no longer cut off after 100 s. The uploaded stream is not disposed.
- 403 Forbidden throws `YandexApiException`, not `NotAuthorizedException`; only 401 does.
- Query values are escaped per RFC 3986 (a space is `%20`, not `+`); media types no longer depend on the current culture.
- Protocol models are nullable-annotated: `Resource.Embedded`, `PublicUrl`, `Md5` and other optional fields are nullable. `Resource` gained `File`, `Sha256`, `ResourceId`, `Revision`, `MediaType`, `AntivirusStatus`; `Disk` gained `MaxFileSize`, `PaidMaxFileSize`, `IsPaid`, `Revision`.

**From the original `YandexDisk.Client`.** This is a fork of [raidenyn/yandexdisk.client](https://github.com/raidenyn/yandexdisk.client). The API shape is the same, but request types use `required` properties, and the JSON layer moved from Newtonsoft.Json to source-generated `System.Text.Json`.

## Building

```shell
dotnet build src/YandexDisk.Client.sln
dotnet test src/YandexDisk.Client.sln
```

The repository also has `YandexDisk.Client.CLI`, a small sample console app for uploading and downloading files. It is not published to NuGet.

Releases: push a `vX.Y.Z` tag. [`publish.yml`](.github/workflows/publish.yml) builds, tests and pushes the package to NuGet.org via Trusted Publishing, with the version taken from the tag.

## History and license

Originally written by Yuriy Nagaev as [YandexDisk.Client](https://github.com/raidenyn/yandexdisk.client). Forked by Egor Zheludkov as `Egorozh.YandexDisk.Client` ([egorozh/YandexDisk.Client.Net](https://github.com/egorozh/YandexDisk.Client.Net)) to support Native AOT. Maintained by XFiler Community since 9.0.0.

[MIT](LICENSE)
