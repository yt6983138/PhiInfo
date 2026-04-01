using System.CommandLine;

namespace PhiInfo.CLI;

internal class Program
{
	private static readonly Option<FileInfo> ApkOption = new("--apk")
	{
		Description = "Path to the APK file",
		Required = true
	};
	private static readonly Option<FileInfo> ClassDataOption = new("--classdata")
	{
		Description = "Path to the class data TPK file",
		DefaultValueFactory = _ => new FileInfo("./classdata.tpk")
	};

	private static readonly ManualResetEventSlim ExitEvent = new(false);

	private static int Main(string[] args)
	{


#pragma warning disable IDE0028 // Simplify collection initialization
		RootCommand rootCommand = new("PhiInfo HTTP Server CLI");
		// visual studio is having some problem with above line
#pragma warning restore IDE0028 // Simplify collection initialization
		rootCommand.Options.Add(ApkOption);
		rootCommand.Options.Add(ClassDataOption);


		rootCommand.SetAction(AfterArgumentParsed);
		int exitCode = rootCommand.Parse(args).Invoke();

		ExitEvent.Dispose();

		return exitCode;
	}

	private static void AfterArgumentParsed(ParseResult parseResult)
	{
		FileInfo? apkFile = parseResult.GetValue(ApkOption);
		FileInfo? classDataFile = parseResult.GetValue(ClassDataOption);

		if (apkFile is not { Exists: true })
		{
			Console.WriteLine($"Error: APK file not found: {apkFile?.FullName ?? "<null>"}");
			return;
		}

		if (classDataFile is not { Exists: true })
		{
			Console.WriteLine($"Error: Class data file not found: {classDataFile?.FullName ?? "<null>"}");
			return;
		}


		using FileStream cldb = File.OpenRead(classDataFile.FullName);

		// 注册事件
		Console.CancelKeyPress += OnCancelKeyPress;


		ExitEvent.Wait();


		Console.WriteLine("[System] Server stopped successfully.");
		return;

		static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
		{
			e.Cancel = true;

			Console.WriteLine("\n[System] Shutdown signal received.");
			Console.WriteLine("[System] Stopping server...");

			ExitEvent.Set();
		}
	}
}