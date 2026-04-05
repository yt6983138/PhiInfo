using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using PhiInfo.Processing;
using PhiInfo.Processing.Type;
using Shua.Zip;

namespace PhiInfo.CLI;

internal class Program
{
    private static AppInfo GetAppInfo()
    {
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "Unknown";
        return new AppInfo(version, "CLI");
    }

    private static int Main(string[] args)
    {
        Option<FileInfo[]> packagesOption = new("--package")
        {
            Aliases = { "-p" },
            Description = """
                          Path to package files. A package file can be APK, main OBB, or patch OBB. 
                          If your copy of Phigros is downloaded from Google play require all of those 
                          or the first two files.
                          If your copy of Phigros is downloaded from TapTap, you only need to provide 
                          the APK file, since TapTap's APK already contains all the data.
                          """,
            Required = true
        };

        Option<FileInfo> classDataOption = new("--classdata")
        {
            Aliases = { "-cldb" },
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

        RootCommand rootCommand = new("PhiInfo HTTP Server CLI");
        rootCommand.Options.Add(packagesOption);
        rootCommand.Options.Add(classDataOption);
        rootCommand.Options.Add(portOption);
        rootCommand.Options.Add(hostOption);

        using var exitEvent = new ManualResetEventSlim(false);

        rootCommand.SetAction(parseResult =>
        {
            var packages = parseResult.GetValue(packagesOption)!;
            var classDataFile = parseResult.GetValue(classDataOption);
            var port = parseResult.GetValue(portOption);
            var host = parseResult.GetValue(hostOption);

            var anyNotFound = packages.FirstOrDefault(p => !p.Exists);
            if (anyNotFound is not null)
            {
                Console.WriteLine($"Error: Package file not found: {anyNotFound.FullName}");
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

            using var server = PhiInfoHttpServer.FromAndroidPackagesPathAndCldb(
                packages.Select(p => new ShuaZip(new MmapReadAt(p.FullName))), File.OpenRead(classDataFile.FullName),
                GetAppInfo(), port, host);

            server.OnRequestError += (sender, ex) => { Console.WriteLine($"Server error: {ex}"); };

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

                exitEvent.Set();
            }
        });

        return rootCommand.Parse(args).Invoke();
    }
}