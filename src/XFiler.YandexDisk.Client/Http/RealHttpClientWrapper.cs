using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XFiler.YandexDisk.Client.Http;

internal sealed class RealHttpClientWrapper(HttpClient httpClient, bool ownsHttpClient) : IHttpClient
{
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        => httpClient.SendAsync(request, completionOption, cancellationToken);

    public void Dispose()
    {
        if (ownsHttpClient)
            httpClient.Dispose();
    }
}
