namespace Chronofoil.Capture.IO;

public class SimpleBuffer
{
	private readonly byte[] _buffer;
	private int _offset;

	public SimpleBuffer(int size)
	{
		_buffer = new byte[size];
		_offset = 0;
	}
	
	public int Size => _offset;

	public Span<byte> Get(int offset, int length)
	{
		return _buffer.AsSpan()[offset..(offset + length)];
	}

	public void Write(ReadOnlySpan<byte> src)
	{
		if (_offset + src.Length > _buffer.Length)
			throw new ArgumentException("Src length must be less than the remaining size of the buffer.");

		// DalamudApi.PluginLog.Debug($"Writing {src.Length} bytes to buffer starting at {_offset}");
		var dstSlice = _buffer.AsSpan().Slice(_offset, src.Length);
		src.CopyTo(dstSlice);
		_offset += src.Length;
	}
	
	public void WriteNull(int count)
	{
		if (_offset + count > _buffer.Length)
			throw new ArgumentException("Src length must be less than the remaining size of the buffer.");
		
		var dstSlice = _buffer.AsSpan().Slice(_offset, count);
		for (int i = 0; i < count; i++) dstSlice[i] = 0;
		_offset += count;
	}

	public void Clear()
	{
		_offset = 0;
	}

	public ReadOnlySpan<byte> GetBuffer()
	{
		return _buffer.AsSpan()[.._offset];
	}
}