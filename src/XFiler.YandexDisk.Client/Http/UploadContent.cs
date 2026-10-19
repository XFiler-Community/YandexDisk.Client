using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XFiler.YandexDisk.Client.Http;

/// <summary>
/// Streams the caller's stream to the request with optional progress. Unlike StreamContent,
/// it does not dispose the stream, and rewinds a seekable one if the request is resent.
/// </summary>
internal sealed class UploadContent : HttpContent
{
    private const int BufferSize = 81920;

    private readonly Stream _source;
    private readonly long _startPosition;
    private readonly long? _length;
    private readonly Action<long>? _bytesSent;

    public UploadContent(Stream source, long? length, Action<long>? bytesSent)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(source));

        _source = source;
        _startPosition = source.CanSeek ? source.Position : -1;
        _length = length ?? (source.CanSeek ? source.Length - source.Position : null);
        _bytesSent = bytesSent;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context,
        CancellationToken cancellationToken)
    {
        if (_startPosition >= 0)
            _source.Position = _startPosition;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            long sent = 0;
            int read;

            while ((read = await _source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

                sent += read;
                _bytesSent?.Invoke(sent);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _length ?? -1;

        return _length.HasValue;
    }
}
