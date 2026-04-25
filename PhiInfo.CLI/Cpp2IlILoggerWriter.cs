using LibCpp2IL.Logging;
using Microsoft.Extensions.Logging;

namespace PhiInfo.CLI;

/// <summary>
/// Logger to proxy cpp2il logs to ILogger. Note that cpp2il logs may contain trailing newlines, so we trim them before forwarding to ILogger.
/// </summary>
public class Cpp2IlILoggerWriter : LogWriter
{
	private ILogger<Cpp2IlILoggerWriter> _logger;

	/// <summary>
	/// Construct a cpp2il logger proxy using a supplied logger.
	/// Please set <see cref="LibLogger.Writer"/> for this to work.
	/// </summary>
	/// <param name="logger">The logger to forward to.</param>
	public Cpp2IlILoggerWriter(ILogger<Cpp2IlILoggerWriter> logger)
	{
		this._logger = logger;
	}

	/// <inheritdoc/>
	public override void Error(string message) =>
		this._logger.LogError(message.TrimEnd());

	/// <inheritdoc/>
	public override void Info(string message) =>
		this._logger.LogInformation(message.TrimEnd());

	/// <inheritdoc/>
	public override void Verbose(string message) =>
		this._logger.LogDebug(message.TrimEnd());

	/// <inheritdoc/>
	public override void Warn(string message) =>
		this._logger.LogWarning(message.TrimEnd());
}
