using Microsoft.AspNetCore.Http.Features;

namespace Cale.Api.Middleware;

/// <summary>Write-only stream wrapper that counts the bytes passed to the inner response body.</summary>
public sealed class CountingWriteStream : Stream
{
    private readonly Stream _inner;
    private long _written;

    public CountingWriteStream(Stream inner) => _inner = inner;

    public long BytesWritten => Interlocked.Read(ref _written);

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        Interlocked.Add(ref _written, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        Interlocked.Add(ref _written, buffer.Length);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        Interlocked.Add(ref _written, count);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken);
        Interlocked.Add(ref _written, buffer.Length);
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>
/// Response body feature that routes every write (Stream, PipeWriter and SendFile fallback) through a
/// <see cref="CountingWriteStream"/>, the same technique ASP.NET's response compression uses.
/// </summary>
public sealed class CountingResponseBodyFeature : StreamResponseBodyFeature
{
    public CountingResponseBodyFeature(IHttpResponseBodyFeature prior)
        : this(new CountingWriteStream(prior.Stream), prior)
    {
    }

    private CountingResponseBodyFeature(CountingWriteStream counter, IHttpResponseBodyFeature prior)
        : base(counter, prior) => Counter = counter;

    public CountingWriteStream Counter { get; }
}
