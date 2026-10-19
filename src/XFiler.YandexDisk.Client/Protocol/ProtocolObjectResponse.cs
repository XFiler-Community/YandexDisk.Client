using System.Net;
using System.Text.Json.Serialization;

namespace XFiler.YandexDisk.Client.Protocol;

/// <summary>
/// Base class of protocol object
/// </summary>
public class ProtocolObjectResponse
{
    /// <summary>
    /// Http status code of response from Yandex Disk API
    /// </summary>
    /// <remarks>Set from the HTTP response, not part of the JSON body.</remarks>
    [JsonIgnore]
    public HttpStatusCode HttpStatusCode { get; set; }
}
