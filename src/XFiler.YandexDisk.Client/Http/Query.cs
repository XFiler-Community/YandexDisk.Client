using System;
using System.Globalization;
using System.Text;

namespace XFiler.YandexDisk.Client.Http;

/// <summary>
/// Builds "?key=value&amp;..." skipping null values. Keys are literals, values are escaped per RFC 3986.
/// </summary>
internal sealed class Query
{
    private readonly StringBuilder _builder = new();

    public Query Add(string key, string? value)
    {
        if (value is null)
            return this;

        _builder
            .Append(_builder.Length == 0 ? '?' : '&')
            .Append(key)
            .Append('=')
            .Append(Uri.EscapeDataString(value));

        return this;
    }

    public Query Add(string key, bool value) => Add(key, value ? "true" : "false");

    public Query Add(string key, int? value) =>
        value is null ? this : Add(key, value.Value.ToString(CultureInfo.InvariantCulture));

    public override string ToString() => _builder.ToString();
}
