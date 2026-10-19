using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XFiler.YandexDisk.Client.Protocol;

internal sealed class CustomPropertiesRequest
{
    [JsonPropertyName("custom_properties")]
    public required IDictionary<string, string?> CustomProperties { get; init; }
}
