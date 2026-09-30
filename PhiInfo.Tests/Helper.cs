namespace PhiInfo.Tests;

internal static class Helper
{
	private static volatile bool _hasSetWorkingDirectory = false;

	internal static string[] TestPackages => [
		"./TestData/base.apk",
		"./TestData/obb.obb",
		"./TestData/auxObb.obb"
	];
	internal static Stream[] GetTestPackageStreams() => TestPackages.Where(File.Exists).Select(File.OpenRead).ToArray();

	internal static string TestClassDataTPKPath => "./TestData/classdata.tpk";
	internal static Stream GetClassDataTPKStream() => File.OpenRead(TestClassDataTPKPath);

	internal static void EnsureWorkingDirectory()
	{
		if (Interlocked.Exchange(ref _hasSetWorkingDirectory, true)) return;
		Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, "..", "..", "..");
	}
}
