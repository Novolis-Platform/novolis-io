using System.Buffers;

namespace Novolis.IO.Ndjson;

internal sealed class PooledByteBuffer(int maximumLength) : IDisposable
{
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(Math.Min(maximumLength, 16 * 1024));
    private int _length;
    private bool _exceeded;

    public int Length => _length;

    public bool IsExceeded => _exceeded;

    public void Append(byte value)
    {
        if (_exceeded)
            return;

        if (_length >= maximumLength)
        {
            _exceeded = true;
            return;
        }

        EnsureCapacity(_length + 1);
        _buffer[_length++] = value;
    }

    public ReadOnlyMemory<byte> GetContent()
    {
        var length = _length;
        if (length > 0 && _buffer[length - 1] == (byte)'\r')
            length--;

        return _buffer.AsMemory(0, length);
    }

    public void Reset()
    {
        _length = 0;
        _exceeded = false;
    }

    public void Dispose() => ArrayPool<byte>.Shared.Return(_buffer);

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
            return;

        var nextLength = Math.Min(maximumLength, Math.Max(required, _buffer.Length * 2));
        var next = ArrayPool<byte>.Shared.Rent(nextLength);
        _buffer.AsSpan(0, _length).CopyTo(next);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = next;
    }
}
