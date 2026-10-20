using System;
using System.IO;

namespace YandexDisk.Client.Tests.Integration;

/// <summary>
/// A large file without a large buffer: deterministic pseudo-random bytes of a given length.
/// Seekable to the start only, which is all an upload needs to resend.
/// </summary>
internal sealed class GeneratedStream(long length, ulong seed) : Stream
{
    private readonly ulong _seed = seed;
    private ulong _state = seed;
    private long _position;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => length;

    public override long Position
    {
        get => _position;
        set
        {
            if (value != 0)
                throw new NotSupportedException("Only rewinding to the start is supported.");

            _position = 0;
            _state = _seed;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int count = (int)Math.Min(buffer.Length, length - _position);

        for (int i = 0; i < count; i++)
        {
            // xorshift64
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            buffer[i] = (byte)_state;
        }

        _position += count;

        return count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (offset != 0 || origin != SeekOrigin.Begin)
            throw new NotSupportedException("Only rewinding to the start is supported.");

        Position = 0;

        return 0;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
