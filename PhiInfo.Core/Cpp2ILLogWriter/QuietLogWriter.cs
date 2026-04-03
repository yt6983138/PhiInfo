using LibCpp2IL.Logging;

namespace PhiInfo.Core.Cpp2ILLogWriter;
public class QuietLogWriter : LogWriter
{
	public override void Error(string message) { }
	public override void Info(string message) { }
	public override void Verbose(string message) { }
	public override void Warn(string message) { }
}
