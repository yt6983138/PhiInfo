using LibCpp2IL.Logging;
using System.Text;

namespace PhiInfo.Core;
public class StreamLogWriter : LogWriter, IDisposable
{
	public Stream Stream { get; set; }
	public Encoding Encoding { get; set; }

	public StreamLogWriter(Stream stream, Encoding encoding)
	{
		this.Stream = stream;
		this.Encoding = encoding;
	}
	~StreamLogWriter()
	{
		this.Dispose();
	}

	private void Write(string message)
	{
		byte[] bytes = this.Encoding.GetBytes(message);
		this.Stream.Write(bytes);
	}

	public override void Info(string message)
		=> this.Write(message);
	public override void Warn(string message)
		=> this.Write(message);
	public override void Error(string message)
		=> this.Write(message);
	public override void Verbose(string message)
		=> this.Write(message);

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		this.Stream.Dispose();
	}
}
