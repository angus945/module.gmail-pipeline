namespace GmailPipeline.Google.Mime;

internal sealed class ReadOnlyMemoryStream : Stream
{
    private readonly ReadOnlyMemory<byte> _content;
    private int _position;

    public ReadOnlyMemoryStream(ReadOnlyMemory<byte> content)
    {
        _content = content;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _content.Length;

    public override long Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (value > _content.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _position = (int)value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = Read(buffer.AsSpan(offset, count));
        return bytesRead;
    }

    public override int Read(Span<byte> buffer)
    {
        var remaining = _content.Length - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        var bytesToRead = Math.Min(buffer.Length, remaining);
        _content.Span.Slice(_position, bytesToRead).CopyTo(buffer);
        _position += bytesToRead;
        return bytesToRead;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _content.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        Position = target;
        return _position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
