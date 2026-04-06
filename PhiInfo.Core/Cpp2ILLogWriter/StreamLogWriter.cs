using LibCpp2IL.Logging;
using System.Text;

namespace PhiInfo.Core.Cpp2ILLogWriter;

/// <summary>
/// A log writer that writes to a stream. Use this to forward logs to a file or other stream. 
/// Logger can be set by setting <see cref="LibLogger.Writer"/>.s
/// </summary>
public class StreamLogWriter : LogWriter, IDisposable
{
	/// <summary>
	/// Stream to forward logs to. The stream will be disposed when this log writer is disposed.
	/// </summary>
	public Stream Stream { get; set; }
	/// <summary>
	/// Encoding to use when writing logs to the stream. Defaults to UTF-8.
	/// </summary>
	public Encoding Encoding { get; set; }

	/// <summary>
	/// Constructs a new <see cref="StreamLogWriter"/> with the given stream and encoding. 
	/// The stream will be disposed when this log writer is disposed.
	/// </summary>
	/// <param name="stream">Stream to forward logs to.</param>
	/// <param name="encoding">Encoding for the forwarded logs.</param>
	public StreamLogWriter(Stream stream, Encoding encoding)
	{
		this.Stream = stream;
		this.Encoding = encoding;
	}
	/// <summary>
	/// Finalizes this log writer by disposing it. This will dispose the stream as well.
	/// </summary>
	~StreamLogWriter()
	{
		this.Dispose();
	}

	private void Write(string message)
	{
		byte[] bytes = this.Encoding.GetBytes(message);
		this.Stream.Write(bytes);
	}

	/// <inheritdoc/>
	public override void Info(string message)
		=> this.Write(message);
	/// <inheritdoc/>
	public override void Warn(string message)
		=> this.Write(message);
	/// <inheritdoc/>
	public override void Error(string message)
		=> this.Write(message);
	/// <inheritdoc/>
	public override void Verbose(string message)
		=> this.Write(message);

	/// <inheritdoc/>
	public void Dispose()
	{
		GC.SuppressFinalize(this);
		this.Stream.Dispose();
	}
}
