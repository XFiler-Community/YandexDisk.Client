using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XFiler.YandexDisk.Client;
using XFiler.YandexDisk.Client.Clients;
using XFiler.YandexDisk.Client.Http;
using XFiler.YandexDisk.Client.Protocol;

namespace YandexDisk.Client.Tests;

/// <summary>
/// Behavior fixed in 9.0.0 that earlier versions got wrong.
/// </summary>
public class RegressionTests
{
    private const string OperationLink = """
        { "href": "http://ya.ru/api/operations/42", "method": "GET", "templated": false }
        """;

    private static readonly TimeSpan FastPoll = TimeSpan.FromMilliseconds(5);

    [Test]
    public async Task Query_EscapesSpacePlusAndCyrillicPerRfc3986()
    {
        const string path = "/Мои файлы/a+b.txt";
        var http = new FakeHttpClient().Respond(HttpStatusCode.OK, """{ "name": "a+b.txt", "path": "disk:/x", "type": "file" }""");

        await http.CreateApi().MetaInfo.GetInfoAsync(new ResourceRequest { Path = path });

        // HttpUtility.UrlEncode turned the space into "+", which the API reads back as a literal plus.
        Assert.AreEqual(FakeHttpClient.BaseUrl + "resources?path=" + Uri.EscapeDataString(path), http.Requests[0].Url);
        StringAssert.Contains("%20", http.Requests[0].Url);
        StringAssert.Contains("%2B", http.Requests[0].Url);
    }

    [Test]
    public async Task MediaType_IsLowerCaseInTurkishCulture()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

        try
        {
            var http = new FakeHttpClient().Respond(HttpStatusCode.OK, """{ "items": [], "limit": 20, "offset": 0 }""");

            await http.CreateApi().MetaInfo.GetFilesInfoAsync(new FilesResourceRequest { MediaType = [MediaType.Image] });

            // "Image".ToLower() is "ımage" (dotless i) in tr-TR.
            StringAssert.EndsWith("resources/files?media_type=image", http.Requests[0].Url);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Test]
    public void NonJsonErrorBody_ThrowsYandexApiException()
    {
        var http = new FakeHttpClient().Respond(HttpStatusCode.BadGateway, "<html>502 Bad Gateway</html>", "text/html");

        var e = Assert.ThrowsAsync<YandexApiException>(() => http.CreateApi().MetaInfo.GetDiskInfoAsync());

        Assert.AreEqual(HttpStatusCode.BadGateway, e!.StatusCode);
        Assert.IsNull(e.Error);
    }

    [Test]
    public void JsonErrorBody_IsParsed()
    {
        var http = new FakeHttpClient().Respond(HttpStatusCode.NotFound,
            """{ "error": "DiskNotFoundError", "description": "Resource not found." }""");

        var e = Assert.ThrowsAsync<YandexApiException>(() =>
            http.CreateApi().MetaInfo.GetInfoAsync(new ResourceRequest { Path = "/missing" }));

        Assert.AreEqual(HttpStatusCode.NotFound, e!.StatusCode);
        Assert.AreEqual("DiskNotFoundError", e.Error!.Error);
        Assert.AreEqual("Resource not found.", e.Message);
    }

    [Test]
    public void Unauthorized_ThrowsNotAuthorized_ForbiddenDoesNot()
    {
        var http = new FakeHttpClient()
            .Respond(HttpStatusCode.Unauthorized, """{ "error": "UnauthorizedError" }""")
            .Respond(HttpStatusCode.Forbidden, """{ "error": "DiskResourceDownloadLimitExceededError" }""");
        DiskHttpApi api = http.CreateApi();

        Assert.ThrowsAsync<NotAuthorizedException>(() => api.MetaInfo.GetDiskInfoAsync());

        var forbidden = Assert.ThrowsAsync<YandexApiException>(() => api.MetaInfo.GetDiskInfoAsync());
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden!.StatusCode);
    }

    [Test]
    public async Task CopyAndWait_PollsUntilSuccess()
    {
        var http = new FakeHttpClient()
            .Respond(HttpStatusCode.Accepted, OperationLink)
            .RespondOperation("in-progress")
            .RespondOperation("in-progress")
            .RespondOperation("success");

        await http.CreateApi().Commands.CopyAndWaitAsync(
            new CopyFileRequest { From = "/a", Path = "/b" }, CancellationToken.None, FastPoll);

        Assert.AreEqual(4, http.Requests.Count);
        Assert.AreEqual("http://ya.ru/api/operations/42", http.Requests[3].Url);
    }

    [Test]
    public void MoveAndWait_FailedOperationThrows()
    {
        var http = new FakeHttpClient()
            .Respond(HttpStatusCode.Accepted, OperationLink)
            .RespondOperation("failure");

        // Used to return as if the move had succeeded.
        var e = Assert.ThrowsAsync<YandexOperationFailedException>(() => http.CreateApi().Commands.MoveAndWaitAsync(
            new MoveFileRequest { From = "/a", Path = "/b" }, CancellationToken.None, FastPoll));

        Assert.AreEqual("http://ya.ru/api/operations/42", e!.Operation.Href);
    }

    [Test]
    public void DeleteAndWait_CancellationThrows()
    {
        using var cts = new CancellationTokenSource();
        var http = new FakeHttpClient()
            .Respond(HttpStatusCode.Accepted, OperationLink)
            .Respond((_, _) =>
            {
                cts.Cancel();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "status": "in-progress" }""")
                });
            });

        // Used to return silently, leaving the caller to think the delete was done.
        Assert.CatchAsync<OperationCanceledException>(() => http.CreateApi().Commands.DeleteAndWaitAsync(
            new DeleteFileRequest { Path = "/a" }, cts.Token, FastPoll));
    }

    [Test]
    public async Task WaitForOperation_DoesNotBlockTheCallingThread()
    {
        var http = new FakeHttpClient()
            .Respond(HttpStatusCode.Accepted, OperationLink)
            .RespondOperation("success");

        Task wait = http.CreateApi().Commands.CopyAndWaitAsync(
            new CopyFileRequest { From = "/a", Path = "/b" }, CancellationToken.None, TimeSpan.FromMilliseconds(200));

        // Thread.Sleep made the call itself block for the whole poll interval.
        Assert.IsFalse(wait.IsCompleted);

        await wait;
    }

    [Test]
    public async Task EmptyTrash_NoContent_ReturnsNullAndWaitDoesNotThrow()
    {
        var http = new FakeHttpClient()
            .Respond(HttpStatusCode.NoContent)
            .Respond(HttpStatusCode.NoContent);
        DiskHttpApi api = http.CreateApi();

        Assert.IsNull(await api.Commands.EmptyTrashAsync());
        await api.Commands.EmptyTrashAndWaitAsync();

        Assert.AreEqual(FakeHttpClient.BaseUrl + "trash/resources", http.Requests[0].Url);
    }

    [Test]
    public async Task UploadWithProgress_ReportsFractionAndKeepsStreamOpen()
    {
        byte[] data = Enumerable.Range(0, 300_000).Select(i => (byte)i).ToArray();
        byte[]? received = null;
        long? contentLength = null;

        var http = new FakeHttpClient().Respond(async (request, ct) =>
        {
            contentLength = request.Content!.Headers.ContentLength;
            received = await request.Content.ReadAsByteArrayAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var progress = new List<double>();
        var stream = new MemoryStream(data);

        await http.CreateApi().Files.UploadWithProgressAsync(
            new Link { Href = "https://uploader.yandex.net/upload/1", Method = "PUT" },
            stream, data.Length, progress.Add);

        CollectionAssert.AreEqual(data, received);
        Assert.AreEqual(data.Length, contentLength);
        Assert.Greater(progress.Count, 1);
        Assert.AreEqual(1.0, progress[^1]);
        CollectionAssert.IsOrdered(progress);
        Assert.IsTrue(stream.CanRead, "the caller's stream must not be disposed");
    }

    [Test]
    public async Task UploadWithProgress_ZeroSize_ReportsNoNaN()
    {
        var http = new FakeHttpClient().Respond(async (request, ct) =>
        {
            await request.Content!.ReadAsByteArrayAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var progress = new List<double>();

        await http.CreateApi().Files.UploadWithProgressAsync(
            new Link { Href = "https://uploader.yandex.net/upload/1", Method = "PUT" },
            new MemoryStream([1, 2, 3]), fileSize: 0, progress.Add);

        Assert.IsFalse(progress.Any(double.IsNaN));
        Assert.IsFalse(progress.Any(double.IsInfinity));
    }

    [Test]
    public async Task Download_StreamsWithoutBufferingTheBody()
    {
        var http = new FakeHttpClient().Respond(HttpStatusCode.OK, "file content", "application/octet-stream");

        (Stream stream, long length) = await http.CreateApi().Files.DownloadFastAsync(
            new Link { Href = "https://downloader.yandex.net/disk/1", Method = "GET" });

        await using (stream)
        {
            Assert.AreEqual("file content", await new StreamReader(stream).ReadToEndAsync());
        }

        Assert.AreEqual(12, length);
        Assert.AreEqual(HttpCompletionOption.ResponseHeadersRead, http.Requests[0].CompletionOption);
    }

    [Test]
    public void ApiTimeout_ThrowsTimeoutException()
    {
        var http = new FakeHttpClient().Respond(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

        DiskHttpApi api = http.CreateApi();
        api.ApiTimeout = TimeSpan.FromMilliseconds(50);

        Assert.ThrowsAsync<TimeoutException>(() => api.MetaInfo.GetDiskInfoAsync());
    }

    [Test]
    public async Task Resource_ReadsNewFieldsAndIgnoresUnknown()
    {
        var http = new FakeHttpClient().Respond(HttpStatusCode.OK, """
            {
              "name": "photo.png", "path": "disk:/photo.png", "type": "file",
              "file": "https://downloader.disk.yandex.ru/disk/abc",
              "sha256": "e3b0c4", "resource_id": "123:abc", "revision": 1700000000000000,
              "media_type": "image", "antivirus_status": "clean",
              "exif": { "date_time": "2024-01-01T00:00:00+00:00" }, "some_future_field": [1, 2]
            }
            """);

        Resource resource = await http.CreateApi().MetaInfo.GetInfoAsync(new ResourceRequest { Path = "/photo.png" });

        Assert.AreEqual(ResourceType.File, resource.Type);
        Assert.AreEqual("https://downloader.disk.yandex.ru/disk/abc", resource.File);
        Assert.AreEqual("e3b0c4", resource.Sha256);
        Assert.AreEqual("123:abc", resource.ResourceId);
        Assert.AreEqual(1700000000000000, resource.Revision);
        Assert.AreEqual("image", resource.MediaType);
        Assert.AreEqual(HttpStatusCode.OK, resource.HttpStatusCode);
        Assert.IsNull(resource.Embedded);
    }

    [Test]
    public async Task SharedHttpClient_TokenPerRequest_RedactedInLogs_NoFileBodyLogged()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var logs = new LogSaver();

        using (var api = new DiskHttpApi(httpClient, "secret-token", logs))
        {
            await api.Files.UploadAsync(new Link { Href = "https://uploader.yandex.net/upload/1", Method = "PUT" },
                new MemoryStream(Encoding.UTF8.GetBytes("file body")));
        }

        Assert.AreEqual("OAuth secret-token", handler.Authorization);
        Assert.IsNull(httpClient.DefaultRequestHeaders.Authorization, "a shared HttpClient must not be changed");
        Assert.DoesNotThrow(() => _ = httpClient.Timeout, "a shared HttpClient must not be disposed");

        Assert.AreEqual(1, logs.Requests.Count);
        StringAssert.DoesNotContain("secret-token", logs.Requests[0].Headers);
        StringAssert.Contains("<redacted>", logs.Requests[0].Headers);
        Assert.IsNull(logs.Requests[0].Body);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Authorization = request.Headers.Authorization?.ToString();
            await request.Content!.ReadAsByteArrayAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }

    private sealed class LogSaver : ILogSaver
    {
        public List<RequestLog> Requests { get; } = [];

        public void SaveLog(RequestLog requestLog, ResponseLog responseLog) => Requests.Add(requestLog);
    }
}
