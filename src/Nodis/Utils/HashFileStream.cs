using System.Security.Cryptography;

namespace Nodis.Utils;

public class HashFileStream(string filePath, HashAlgorithm algorithm) : Stream
{
    public override bool CanRead => fileStream.CanRead;
    public override bool CanSeek => fileStream.CanSeek;
    public override bool CanWrite => fileStream.CanWrite;
    public override long Length => fileStream.Length;
    public override long Position
    {
        get => fileStream.Position;
        set => fileStream.Position = value;
    }

    private readonly FileStream fileStream = File.Create(filePath);

    public override void Flush()
    {
        fileStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return fileStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return fileStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        fileStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        algorithm.TransformBlock(buffer, offset, count, buffer, offset);
        fileStream.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // algorithm.TransformBlock(buffer.Span, buffer.Span);
        return base.WriteAsync(buffer, cancellationToken);
    }
}