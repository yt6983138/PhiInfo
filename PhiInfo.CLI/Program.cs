using global.PhiInfo.HttpServer.Type;
using System.CommandLine;
using System.Reflection;

namespace PhiInfo.CLI;

public class CliHttpServer(string apkPath, Stream cldbStream) : HttpServer(apkPath, cldbStream)
{
	protected override AppInfo GetAppInfo()
	{
		string version = typeof(CliHttpServer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
				?.InformationalVersion ?? "Unknown";
		return new AppInfo(version, "CLI");
	}
}

internal class Program
{
	private static int Main(string[] args)
	{
		Option<FileInfo> apkOption = new("--apk")
		{
			Description = "Path to the APK file",
			Required = true
		};

		Option<FileInfo> classDataOption = new("--classdata")
		{
			Description = "Path to the class data TPK file",
			DefaultValueFactory = _ => new FileInfo("./classdata.tpk")
		};

		Option<uint> portOption = new("--port")
		{
			Description = "Port number for the HTTP server",
			DefaultValueFactory = _ => 41669
		};

		Option<string> hostOption = new("--host")
		{
			Description = "Host for the HTTP server",
			DefaultValueFactory = _ => "127.0.0.1"
		};

#pragma warning disable IDE0028 // Simplify collection initialization
		RootCommand rootCommand = new("PhiInfo HTTP Server CLI");
		// visual studio is having some problem with above line
#pragma warning restore IDE0028 // Simplify collection initialization
		rootCommand.Options.Add(apkOption);
		rootCommand.Options.Add(classDataOption);
		rootCommand.Options.Add(portOption);
		rootCommand.Options.Add(hostOption);

		using ManualResetEventSlim exitEvent = new(false);

		rootCommand.SetAction(parseResult =>
		{
			FileInfo? apkFile = parseResult.GetValue(apkOption);
			FileInfo? classDataFile = parseResult.GetValue(classDataOption);
			uint port = parseResult.GetValue(portOption);
			string? host = parseResult.GetValue(hostOption);

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

			if (host == null)
			{
				Console.WriteLine("Error: Host is null");
				return;
			}

			using FileStream cldb = File.OpenRead(classDataFile.FullName);
			using CliHttpServer server = new(apkFile.FullName, cldb);

			_ = server.Start(port, host);

			// 注册事件
			Console.CancelKeyPress += OnCancelKeyPress;

			Console.WriteLine("--------------------------------------------");
			Console.WriteLine($"Server is running on http://{host}:{port}/");
			Console.WriteLine("Press Ctrl+C to stop the server.");
			Console.WriteLine("--------------------------------------------");

			exitEvent.Wait();


			Console.WriteLine("[System] Server stopped successfully.");
			return;

			void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
			{
				e.Cancel = true;

				Console.WriteLine("\n[System] Shutdown signal received.");
				Console.WriteLine("[System] Stopping server...");

				server.Stop();
				exitEvent.Set();
			}
		});

		return rootCommand.Parse(args).Invoke();
	}
}