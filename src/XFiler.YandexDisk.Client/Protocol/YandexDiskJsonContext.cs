using System.Text.Json.Serialization;

namespace XFiler.YandexDisk.Client.Protocol;

/// <summary>
/// One source-generated context for every protocol type: no reflection, Native AOT and trimming safe.
/// </summary>
[JsonSerializable(typeof(Link))]
[JsonSerializable(typeof(Disk))]
[JsonSerializable(typeof(Resource))]
[JsonSerializable(typeof(FilesResourceList))]
[JsonSerializable(typeof(LastUploadedResourceList))]
[JsonSerializable(typeof(Operation))]
[JsonSerializable(typeof(ErrorDescription))]
[JsonSerializable(typeof(CustomPropertiesRequest))]
internal partial class YandexDiskJsonContext : JsonSerializerContext;
