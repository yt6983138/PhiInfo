namespace PhiInfo.Core;

internal class CustomDisposeProxyStream : Stream
{
	internal Stream BaseStream { get; }
	internal Action Disposer { get; set; }
	internal bool IsDisposed { get; private set; }

	internal CustomDisposeProxyStream(Stream stream, Action disposer)
	{
		this.BaseStream = stream;
		this.Disposer = disposer;
	}

	protected override void Dispose(bool disposing)
	{
		if (this.IsDisposed) return;

		this.Disposer.Invoke();
		this.BaseStream.Dispose();
		this.IsDisposed = true;
	}
	public override ValueTask DisposeAsync()
	{
		if (this.IsDisposed) return ValueTask.CompletedTask;

		this.Disposer.Invoke();
		this.IsDisposed = true;
		return this.BaseStream.DisposeAsync();
	}

	public override bool CanRead => this.BaseStream.CanRead;
	public override bool CanSeek => this.BaseStream.CanSeek;
	public override bool CanWrite => this.BaseStream.CanWrite;
	public override long Length => this.BaseStream.Length;
	public override long Position { get => this.BaseStream.Position; set => this.BaseStream.Position = value; }
	public override bool CanTimeout => this.BaseStream.CanTimeout;
	public override int ReadTimeout { get => this.BaseStream.ReadTimeout; set => this.BaseStream.ReadTimeout = value; }
	public override int WriteTimeout { get => this.BaseStream.WriteTimeout; set => this.BaseStream.WriteTimeout = value; }

	public override void Flush() =>
		this.BaseStream.Flush();
	public override Task FlushAsync(CancellationToken cancellationToken) =>
		this.BaseStream.FlushAsync(cancellationToken);
	public override int Read(byte[] buffer, int offset, int count) =>
		this.BaseStream.Read(buffer, offset, count);
	public override int Read(Span<byte> buffer) =>
		this.BaseStream.Read(buffer);
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		this.BaseStream.ReadAsync(buffer, offset, count, cancellationToken);
	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
		this.BaseStream.ReadAsync(buffer, cancellationToken);
	public override int ReadByte() =>
		this.BaseStream.ReadByte();
	public override long Seek(long offset, SeekOrigin origin) =>
		this.BaseStream.Seek(offset, origin);
	public override void SetLength(long value) =>
		this.BaseStream.SetLength(value);
	public override void Write(byte[] buffer, int offset, int count) =>
		this.BaseStream.Write(buffer, offset, count);
	public override void Write(ReadOnlySpan<byte> buffer) =>
		this.BaseStream.Write(buffer);
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		this.BaseStream.WriteAsync(buffer, offset, count, cancellationToken);
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
		this.BaseStream.WriteAsync(buffer, cancellationToken);
	public override void WriteByte(byte value) =>
		this.BaseStream.WriteByte(value);
	public override void CopyTo(Stream destination, int bufferSize) =>
		this.BaseStream.CopyTo(destination, bufferSize);
	public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
		this.BaseStream.CopyToAsync(destination, bufferSize, cancellationToken);
	public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
		this.BaseStream.BeginRead(buffer, offset, count, callback, state);
	public override int EndRead(IAsyncResult asyncResult) =>
		this.BaseStream.EndRead(asyncResult);
	public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
		this.BaseStream.BeginWrite(buffer, offset, count, callback, state);
	public override void EndWrite(IAsyncResult asyncResult) =>
		this.BaseStream.EndWrite(asyncResult);
	public override void Close() =>
		this.BaseStream.Close();
}
