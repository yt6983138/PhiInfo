using LibCpp2IL.Logging;
using Microsoft.Extensions.Logging;

namespace PhiInfo.CLI;
public class Cpp2IlILoggerWriter : LogWriter
{
	private ILogger<Cpp2IlILoggerWriter> _logger;

	public Cpp2IlILoggerWriter(ILogger<Cpp2IlILoggerWriter> logger)
	{
		this._logger = logger;
	}

	public override void Error(string message) =>
		this._logger.LogError(message.TrimEnd());

	public override void Info(string message) =>
		this._logger.LogInformation(message.TrimEnd());

	public override void Verbose(string message) =>
		this._logger.LogDebug(message.TrimEnd());

	public override void Warn(string message) =>
		this._logger.LogWarning(message.TrimEnd());
}
