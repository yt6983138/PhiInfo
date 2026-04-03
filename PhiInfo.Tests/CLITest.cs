namespace PhiInfo.Tests;

[TestClass]
public class CLITest
{
	[TestMethod]
	public void Information()
	{
		Helper.EnsureWorkingDirectory();
		Assert.AreEqual(0, CLI.Program.Main([
			"--apk", Helper.TestApkPath,
			"--obb", Helper.TestObbPath,
			"--classdata", Helper.TestClassDataTPKPath,
			"--extract-info-to", "./TestData/ExtractedInfo"]));
	}
	[TestMethod]
	public void Asset()
	{
		Helper.EnsureWorkingDirectory();
		Assert.AreEqual(0, CLI.Program.Main([
			"--apk", Helper.TestApkPath,
			"--obb", Helper.TestObbPath,
			"--classdata", Helper.TestClassDataTPKPath,
			"--extract-asset-to", "./TestData/ExtractedAsset",
			"--no-illustration",
			"--no-blur-illustration"]));
	}
}
