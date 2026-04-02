namespace PhiInfo.Tests;
internal static class Helper
{
	private static volatile bool _hasSetWorkingDirectory = false;

	internal static string TestApkPath => "./TestData/base.apk";
	internal static string TestObbPath => "./TestData/obb.obb";
	internal static string TestClassDataTPKPath => "./TestData/classdata.tpk";

	internal static void EnsureWorkingDirectory()
	{
		if (_hasSetWorkingDirectory) return;
		_hasSetWorkingDirectory = true;
		Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, "..", "..", "..");
	}
}
