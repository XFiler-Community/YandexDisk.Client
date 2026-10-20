using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XFiler.YandexDisk.Client;
using XFiler.YandexDisk.Client.Clients;
using XFiler.YandexDisk.Client.Http;
using XFiler.YandexDisk.Client.Protocol;

namespace YandexDisk.Client.Tests.Integration;

/// <summary>
/// Release gate against the real Yandex Disk API. Skipped without a token, excluded in CI.
/// </summary>
/// <remarks>
/// Environment:
/// YANDEX_DISK_TOKEN — OAuth token with disk read/write access (required);
/// YANDEX_DISK_LARGE_FILE_MB — size of the large upload/download, 150 by default;
/// YANDEX_DISK_FOLDER_FILES — files in the copy/move folder, 200 by default.
/// Everything happens in a new folder in the Disk root, deleted permanently at the end.
/// </remarks>
[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class RealApiTests
{
    private const string TokenVariable = "YANDEX_DISK_TOKEN";

    private string _token = null!;
    private DiskHttpApi _disk = null!;
    private string _root = null!;

    [OneTimeSetUp]
    public async Task CreateRoot()
    {
        string? token = Environment.GetEnvironmentVariable(TokenVariable);

        if (string.IsNullOrWhiteSpace(token))
            Assert.Ignore($"{TokenVariable} is not set");

        _token = token;
        _disk = new DiskHttpApi(_token);
        _root = $"/xfiler-yandexdisk-it-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..48];

        await _disk.Commands.CreateDirectoryAsync(_root);
        Log($"Working in disk:{_root}");
    }

    [OneTimeTearDown]
    public async Task DeleteRoot()
    {
        if (_disk is null)
            return;

        try
        {
            await _disk.Commands.DeleteAndWaitAsync(new DeleteFileRequest { Path = _root, Permanently = true });
        }
        finally
        {
            _disk.Dispose();
        }
    }

    [Test]
    public async Task DiskInfo()
    {
        Disk info = await _disk.MetaInfo.GetDiskInfoAsync();

        Assert.Greater(info.TotalSpace, 0);
        Assert.Greater(info.MaxFileSize, 0);
        Log($"Disk: {info.UsedSpace / 1_048_576} of {info.TotalSpace / 1_048_576} MiB used, paid: {info.IsPaid}");
    }

    [Test]
    public async Task SharedHttpClient_Works()
    {
        using var httpClient = new HttpClient();
        using var disk = new DiskHttpApi(httpClient, _token);

        Disk info = await disk.MetaInfo.GetDiskInfoAsync();

        Assert.Greater(info.TotalSpace, 0);
        Assert.IsNull(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Test]
    public void InvalidToken_ThrowsNotAuthorized()
    {
        using var disk = new DiskHttpApi("invalid-" + Guid.NewGuid().ToString("N"));

        var e = Assert.ThrowsAsync<NotAuthorizedException>(() => disk.MetaInfo.GetDiskInfoAsync());

        Assert.AreEqual(HttpStatusCode.Unauthorized, e!.StatusCode);
        Log($"401: {e.Error?.Error} — {e.Message}");
    }

    [Test]
    public void MissingResource_ThrowsNotFound()
    {
        var e = Assert.ThrowsAsync<YandexApiException>(() =>
            _disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = $"{_root}/missing-{Guid.NewGuid():N}" }));

        Assert.AreEqual(HttpStatusCode.NotFound, e!.StatusCode);
        Assert.IsNotNull(e.Error?.Error);
    }

    [Test]
    public async Task SpecialCharactersInPaths_RoundTrip()
    {
        // Space, plus, ampersand, percent, hash, Cyrillic: each one broke some URL encoding at some point.
        string folder = $"{_root}/Мои файлы a+b & 100% #1";
        string file = $"{folder}/тест файл+1 (copy) & [x].txt";
        byte[] content = Encoding.UTF8.GetBytes("Привет, Диск! a+b=c & 100%");

        await _disk.Commands.CreateDirectoryAsync(folder);
        await _disk.Files.UploadFileAsync(file, overwrite: false, new MemoryStream(content));

        Resource listing = await _disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = folder });
        Resource item = listing.Embedded!.Items.Single();

        Assert.AreEqual("тест файл+1 (copy) & [x].txt", item.Name);
        Assert.AreEqual("disk:" + file, item.Path);
        Assert.AreEqual(ResourceType.File, item.Type);
        Assert.AreEqual(content.Length, item.Size);

        await using Stream download = await _disk.Files.DownloadFileAsync(file);
        var copy = new MemoryStream();
        await download.CopyToAsync(copy);

        CollectionAssert.AreEqual(content, copy.ToArray());
    }

    [Test]
    public async Task LargeFile_UploadWithProgress_StreamingDownload()
    {
        long size = ReadLong("YANDEX_DISK_LARGE_FILE_MB", 150) * 1_048_576;
        string path = $"{_root}/large.bin";
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));

        byte[] expectedMd5 = await Md5Async(new GeneratedStream(size, seed: 42));

        // Upload: no HttpClient timeout, progress up to 1.0, the file is never held in memory.
        var progress = new List<double>();
        var upload = Stopwatch.StartNew();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

        Link uploadLink = await _disk.Files.GetUploadLinkAsync(path, overwrite: false, timeout.Token);
        await _disk.Files.UploadWithProgressAsync(uploadLink, new GeneratedStream(size, seed: 42), size,
            p => { lock (progress) progress.Add(p); }, timeout.Token);

        long uploadAllocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        Log($"Upload: {size / 1_048_576} MiB in {upload.Elapsed.TotalSeconds:0} s, " +
            $"{progress.Count} progress reports, {uploadAllocated / 1_048_576} MiB allocated");

        Assert.AreEqual(1.0, progress[^1], 1e-9);
        CollectionAssert.IsOrdered(progress);
        Assert.Less(uploadAllocated, size / 4, "upload must stream, not buffer the file");

        Resource uploaded = await _disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = path }, timeout.Token);
        Assert.AreEqual(size, uploaded.Size);
        Assert.AreEqual(Convert.ToHexString(expectedMd5), uploaded.Md5?.ToUpperInvariant());

        // Download: streamed, hashed on the fly.
        var download = Stopwatch.StartNew();
        allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

        Link downloadLink = await _disk.Files.GetDownloadLinkAsync(path, timeout.Token);
        (Stream stream, long length) = await _disk.Files.DownloadFastAsync(downloadLink, timeout.Token);

        byte[] downloadedMd5;
        await using (stream)
        {
            downloadedMd5 = await Md5Async(stream, timeout.Token);
        }

        long downloadAllocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        Log($"Download: {download.Elapsed.TotalSeconds:0} s, {downloadAllocated / 1_048_576} MiB allocated");

        Assert.AreEqual(size, length);
        CollectionAssert.AreEqual(expectedMd5, downloadedMd5);
        Assert.Less(downloadAllocated, size / 4, "download must stream, not buffer the file");
    }

    [Test]
    public async Task FolderWithManyFiles_CopyAndMove()
    {
        int count = (int)ReadLong("YANDEX_DISK_FOLDER_FILES", 200);
        string source = $"{_root}/many";
        string copy = $"{_root}/many-copy";
        string moved = $"{_root}/many-moved";
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        await _disk.Commands.CreateDirectoryAsync(source, timeout.Token);

        var fill = Stopwatch.StartNew();
        await Parallel.ForEachAsync(Enumerable.Range(0, count),
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = timeout.Token },
            async (i, ct) => await _disk.Files.UploadFileAsync($"{source}/file-{i:D4}.txt", overwrite: false,
                new MemoryStream(Encoding.UTF8.GetBytes($"file {i}")), ct));
        Log($"Uploaded {count} files in {fill.Elapsed.TotalSeconds:0} s");

        // Copy by hand to see whether Yandex went asynchronous (202) for this folder.
        Link copyLink = await _disk.Commands.CopyAsync(new CopyFileRequest { From = source, Path = copy }, timeout.Token);
        Log($"Copy answered {(int)copyLink.HttpStatusCode} {copyLink.HttpStatusCode}");

        if (copyLink.HttpStatusCode == HttpStatusCode.Accepted)
            await _disk.Commands.WaitForOperationAsync(copyLink, timeout.Token);

        Assert.AreEqual(count, await CountAsync(copy, timeout.Token));

        await _disk.Commands.MoveAndWaitAsync(new MoveFileRequest { From = copy, Path = moved }, timeout.Token);

        Assert.AreEqual(count, await CountAsync(moved, timeout.Token));
        Assert.ThrowsAsync<YandexApiException>(() =>
            _disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = copy }, timeout.Token));
    }

    [Test]
    public async Task Trash_DeleteRestoreAndRemoveOnlyOurItem()
    {
        string name = $"trash-{Guid.NewGuid():N}.txt";
        string path = $"{_root}/{name}";

        await _disk.Files.UploadFileAsync(path, overwrite: false, new MemoryStream("in trash"u8.ToArray()));

        bool removedFromTrash = false;

        try
        {
            await _disk.Commands.DeleteAndWaitAsync(new DeleteFileRequest { Path = path });
            Resource trashed = await FindInTrashAsync(name);

            await _disk.Commands.RestoreFromTrashAndWaitAsync(new RestoreFromTrashRequest { Path = trashed.Path });
            Resource restored = await _disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = path });
            Assert.AreEqual(name, restored.Name);

            // Delete it again and remove only this item from the trash, never the user's whole trash.
            await _disk.Commands.DeleteAndWaitAsync(new DeleteFileRequest { Path = path });
            trashed = await FindInTrashAsync(name);
            await _disk.Commands.EmptyTrashAndWaitAsync(trashed.Path);
            removedFromTrash = true;

            Assert.ThrowsAsync<YandexApiException>(() =>
                _disk.MetaInfo.GetTrashInfoAsync(new ResourceRequest { Path = trashed.Path }));
        }
        finally
        {
            if (!removedFromTrash)
                await RemoveFromTrashIfThereAsync(name);
        }
    }

    [Test]
    public async Task CustomPropertiesAndPublishing()
    {
        string path = $"{_root}/published.txt";
        await _disk.Files.UploadFileAsync(path, overwrite: false, new MemoryStream("public"u8.ToArray()));

        Resource withProperties = await _disk.MetaInfo.AppendCustomPropertiesAsync(path,
            new Dictionary<string, string?> { ["owner"] = "xfiler", ["note"] = "a+b & c" });
        Assert.AreEqual("a+b & c", withProperties.CustomProperties!["note"]);

        Resource withoutNote = await _disk.MetaInfo.AppendCustomPropertiesAsync(path,
            new Dictionary<string, string?> { ["note"] = null });
        Assert.IsFalse(withoutNote.CustomProperties?.ContainsKey("note") ?? false);

        await _disk.MetaInfo.PublishAsync(path);
        Resource published = await _disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = path });
        Assert.IsNotNull(published.PublicUrl);

        await _disk.MetaInfo.UnpublishAsync(path);
        Resource unpublished = await _disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = path });
        Assert.IsNull(unpublished.PublicUrl);
    }

    private async Task<int> CountAsync(string folder, CancellationToken cancellationToken)
    {
        Resource info = await _disk.MetaInfo.GetInfoAsync(new ResourceRequest { Path = folder, Limit = 1 },
            cancellationToken);

        return info.Embedded!.Total;
    }

    /// <summary>A failed test must not leave its file in the user's trash.</summary>
    private async Task RemoveFromTrashIfThereAsync(string name)
    {
        try
        {
            Resource trashed = await FindInTrashAsync(name);
            await _disk.Commands.EmptyTrashAndWaitAsync(trashed.Path);
            Log($"Cleanup: removed {trashed.Path} from trash");
        }
        catch (AssertionException)
        {
            // Not in the trash: nothing to clean up.
        }
        catch (Exception e)
        {
            Log($"Cleanup: could not remove {name} from trash, delete it by hand: {e.Message}");
        }
    }

    private async Task<Resource> FindInTrashAsync(string name)
    {
        string originPath = $"disk:{_root}/{name}";
        string? sort = "-deleted"; // newest first; dropped if the API rejects it

        // Trash listing can lag behind the delete for a moment.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            for (int offset = 0; offset < 5_000; offset += 200)
            {
                Resource trash;

                try
                {
                    trash = await _disk.MetaInfo.GetTrashInfoAsync(
                        new ResourceRequest { Path = "/", Limit = 200, Offset = offset, Sort = sort });
                }
                catch (YandexApiException e) when (e.StatusCode == HttpStatusCode.BadRequest && sort is not null)
                {
                    Log($"Trash sort '{sort}' rejected ({e.Message}), listing unsorted");
                    sort = null;
                    offset -= 200;
                    continue;
                }

                Resource? match = trash.Embedded!.Items.FirstOrDefault(r => r.OriginPath == originPath);

                if (match is not null)
                    return match;

                if (trash.Embedded.Items.Count < 200)
                    break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new AssertionException($"{originPath} not found in trash");
    }

    private static async Task<byte[]> Md5Async(Stream stream, CancellationToken cancellationToken = default)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        byte[] buffer = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            md5.AppendData(buffer, 0, read);

        return md5.GetHashAndReset();
    }

    private static long ReadLong(string variable, long defaultValue) =>
        long.TryParse(Environment.GetEnvironmentVariable(variable), out long value) && value > 0 ? value : defaultValue;

    private static void Log(string message) => TestContext.Progress.WriteLine($"[integration] {message}");
}
