using LibCpp2IL.Logging;

namespace PhiInfo.Core.Cpp2ILLogWriter;

/// <summary>
/// A log writer that does nothing. It is meant to be used with LibCpp2Il's 
/// logging system when you want to disable logging. Logger can be set by
/// setting <see cref="LibLogger.Writer"/>.
/// </summary>
public class QuietLogWriter : LogWriter
{
	/// <inheritdoc/>
	public override void Error(string message) { }
	/// <inheritdoc/>
	public override void Info(string message) { }
	/// <inheritdoc/>
	public override void Verbose(string message) { }
	/// <inheritdoc/>
	public override void Warn(string message) { }
}
