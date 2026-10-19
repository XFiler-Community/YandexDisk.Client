# XFiler.YandexDisk.Client

[![CI](https://github.com/XFiler-Community/YandexDisk.Client/actions/workflows/ci.yml/badge.svg)](https://github.com/XFiler-Community/YandexDisk.Client/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/XFiler.YandexDisk.Client.svg)](https://www.nuget.org/packages/XFiler.YandexDisk.Client)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A .NET client for the [Yandex Disk REST API](https://yandex.ru/dev/disk/rest/). It is built for Native AOT and trimming: JSON goes through `System.Text.Json` source generation, there is no reflection, and the package depends only on the BCL. It powers Yandex Disk support in [X-Filer](https://x-filer.com) through [FileVault](https://github.com/XFiler-Community/FileVault).

- Files: upload and download, with progress for uploads and streaming downloads
- Metadata: disk quota, file and folder info with paging and sorting, flat file list, recent uploads, custom properties
- Commands: create folder, copy, move, delete, trash and restore, with helpers that wait for async operations to finish
- Publishing: publish and unpublish files and folders
- `IsAotCompatible`, nullable annotations, `CancellationToken` on every call

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

// One instance per token: it owns an HttpClient.
using IDiskApi disk = new DiskHttpApi(oauthToken);

Disk info = await disk.MetaInfo.GetDiskInfoAsync();
Console.WriteLine($"{info.UsedSpace} of {info.TotalSpace} bytes used");
```

`IDiskApi` has three clients: `Files` (transfer), `MetaInfo` (read metadata), `Commands` (change things).

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

// Stream a download without buffering the whole file
Link downloadLink = await disk.Files.GetDownloadLinkAsync("/Video/video.mp4");
var (content, length) = await disk.Files.DownloadFastAsync(downloadLink);
```

## Files and folders

Copy, move, delete and trash operations on large folders run asynchronously on Yandex's side. The `*AndWaitAsync` helpers poll the operation until it finishes (every 3 seconds by default, see `pullPeriod`).

```csharp
await disk.Commands.CreateDictionaryAsync("/Projects/2026");

await disk.Commands.CopyAndWaitAsync(new CopyFileRequest { From = "/Projects/2025", Path = "/Archive/2025" });
await disk.Commands.MoveAndWaitAsync(new MoveFileRequest { From = "/Inbox/a.txt", Path = "/Docs/a.txt", Overwrite = true });

// To the trash, or Permanently = true to skip it
await disk.Commands.DeleteAndWaitAsync(new DeleteFileRequest { Path = "/Docs/a.txt" });

await disk.Commands.RestoreFromTrashAndWaitAsyncAsync(new RestoreFromTrashRequest { Path = "trash:/a.txt" });
await disk.Commands.EmptyTrashAndWaitAsyncAsync("/");
```

Without the helpers, `CopyAsync`, `MoveAsync` and the rest return a `Link` to the operation. Pass it to `GetOperationStatus` to check on it yourself.

## Publishing

```csharp
await disk.MetaInfo.PublishFolderAsync("/Shared/photos");
Resource shared = await disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = "/Shared/photos" });
Console.WriteLine(shared.PublicUrl);

await disk.MetaInfo.UnpublishFolderAsync("/Shared/photos");
```

## Errors

Failed requests throw `YandexApiException` with `StatusCode`, `ReasonPhrase` and the API's `ErrorDescription`. An invalid or expired token throws its subclass `NotAuthorizedException`.

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

`DiskHttpApi(oauthToken, logSaver)` accepts an `ILogSaver` that receives every request and response. Use it for diagnostics. To test without the network, pass your own `IHttpClient` to `DiskHttpApi(baseUrl, logSaver, httpClient)`.

## Migrating

**From `Egorozh.YandexDisk.Client` 8.x.** The package and namespace were renamed in 9.0.0. Nothing else changed.

```diff
- <PackageReference Include="Egorozh.YandexDisk.Client" Version="8.2.0" />
+ <PackageReference Include="XFiler.YandexDisk.Client" Version="9.0.0" />
```

Then replace `Egorozh.YandexDisk.Client` with `XFiler.YandexDisk.Client` in `using` directives.

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
