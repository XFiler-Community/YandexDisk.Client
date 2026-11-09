using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Http;
using XFiler.YandexDisk.Client.Protocol;
using NUnit.Framework;

namespace YandexDisk.Client.Tests;

public class FilesClientTests
{
    [Test]
    public async Task GetUploadLinkTest()
    {
        var httpClientTest = new TestHttpClient(
            methodName: "GET", 
            url: TestHttpClient.BaseUrl + Url.EscapePath("resources/upload?path=/&overwrite=true"),
            httpStatusCode: HttpStatusCode.OK,
            result:  @"
{
  ""href"": ""https://uploader1d.dst.yandex.net:443/upload-target/..."",
  ""method"": ""PUT"",
  ""templated"": false
}
");

        var diskClient = new DiskHttpApi(TestHttpClient.BaseUrl, 
            logSaver: null,
            httpClient: httpClientTest);

        Link result = await diskClient.Files.GetUploadLinkAsync("/", true, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(result);
        Assert.AreEqual("https://uploader1d.dst.yandex.net:443/upload-target/...", result.Href);
        Assert.AreEqual("PUT", result.Method);
        Assert.AreEqual(false, result.Templated);
    }

    [Test]
    public async Task UploadTest()
    {
        var link = new Link
        {
            Href = "https://uploader1d.dst.yandex.net/upload-target/",
            Method = "PUT",
            Templated = false
        };

        var httpClientTest = new TestHttpClient(
            methodName: link.Method, 
            url: link.Href, 
            httpStatusCode: HttpStatusCode.Created, 
            result: @"");

        var diskClient = new DiskHttpApi(TestHttpClient.BaseUrl,
            logSaver: null,
            httpClient: httpClientTest);

        await diskClient.Files.UploadAsync(link, new MemoryStream(), CancellationToken.None).ConfigureAwait(false);
    }

    [Test]
    public async Task GetDownloadLinkTest()
    {
        var httpClientTest = new TestHttpClient(
            methodName: "GET", 
            url: TestHttpClient.BaseUrl + Url.EscapePath("resources/download?path=/file.txt"),
            httpStatusCode: HttpStatusCode.OK,
            result: @"
{
  ""href"": ""https://downloader.dst.yandex.ru/disk/..."",
  ""method"": ""GET"",
  ""templated"": false
}
");

        var diskClient = new DiskHttpApi(TestHttpClient.BaseUrl,
            logSaver: null,
            httpClient: httpClientTest);

        Link result = await diskClient.Files.GetDownloadLinkAsync("/file.txt", CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(result);
        Assert.AreEqual("https://downloader.dst.yandex.ru/disk/...", result.Href);
        Assert.AreEqual("GET", result.Method);
        Assert.AreEqual(false, result.Templated);
    }

    [Test]
    public async Task DownloadTest()
    {
        var link = new Link
        {
            Href = "https://downloader.dst.yandex.ru/disk/",
            Method = "GET",
            Templated = false
        };

        var httpClientTest = new TestHttpClient(
            methodName: link.Method, 
            url: link.Href, 
            httpStatusCode: HttpStatusCode.Created, 
            result: @"Test file content");

        var diskClient = new DiskHttpApi(TestHttpClient.BaseUrl,
            logSaver: null,
            httpClient: httpClientTest);

        Stream stream = await diskClient.Files.DownloadAsync(link, CancellationToken.None).ConfigureAwait(false);

        var content = new StreamReader(stream).ReadToEnd();

        Assert.AreEqual(@"Test file content", content);
    }

    [Test]
    public async Task DownloadRangeAsync_SendsRangeHeader_AndReportsPartialContent()
    {
        var link = new Link { Href = "https://downloader.dst.yandex.ru/disk/", Method = "GET" };
        string? sentRange = null;

        var http = new FakeHttpClient().Respond((request, _) =>
        {
            sentRange = request.Headers.Range?.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new StringContent("cdef") };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2, 5, 10);
            return Task.FromResult(response);
        });

        var api = new DiskHttpApi(FakeHttpClient.BaseUrl, logSaver: null, httpClient: http);

        (Stream stream, bool isPartial, long? total) = await api.Files.DownloadRangeAsync(link, offset: 2, count: 4);

        Assert.That(sentRange, Is.EqualTo("bytes=2-5"));
        Assert.That(isPartial, Is.True);
        Assert.That(total, Is.EqualTo(10));
        Assert.That(await new StreamReader(stream).ReadToEndAsync(), Is.EqualTo("cdef"));
    }

    [Test]
    public async Task DownloadRangeAsync_ServerIgnoresRange_IsNotPartial()
    {
        var link = new Link { Href = "https://downloader.dst.yandex.ru/disk/", Method = "GET" };
        var http = new FakeHttpClient().Respond(HttpStatusCode.OK, "abcdefghij", "application/octet-stream");
        var api = new DiskHttpApi(FakeHttpClient.BaseUrl, logSaver: null, httpClient: http);

        (Stream stream, bool isPartial, _) = await api.Files.DownloadRangeAsync(link, offset: 2, count: 4);
        await stream.DisposeAsync();

        Assert.That(isPartial, Is.False);
    }
}
