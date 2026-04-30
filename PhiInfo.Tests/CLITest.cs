namespace PhiInfo.Tests;

[TestClass]
public class CLITest
{
	[TestMethod]
	public void Information()
	{
		Helper.EnsureWorkingDirectory();
		Assert.AreEqual(0, CLI.CLI.Main([
			"--apk", Helper.TestApkPath,
			"--obb", Helper.TestObbPath,
			"--classdata", Helper.TestClassDataTPKPath,
			"--extract-info-to", "./TestData/ExtractedInfo",
			"--language", "All",
			"--debug"]));
	}
	[TestMethod]
	public void InformationAuto()
	{
		Helper.EnsureWorkingDirectory();
		Assert.AreEqual(0, CLI.CLI.Main([
			"--download-apk", "TAPTAP",
			"--download-classdata", "AUTO",
			"--apk", Helper.TestApkPath + ".tmp",
			"--obb", Helper.TestObbPath + ".tmp",
			"--classdata", Helper.TestClassDataTPKPath + ".tmp",
			"--extract-info-to", "./TestData/ExtractedInfoAuto",
			"--language", "EnglishUS",
			"--debug"]));
	}
	[TestMethod]
	public void Asset()
	{
		Helper.EnsureWorkingDirectory();

		List<string> args = [
			"--apk", Helper.TestApkPath,
			"--obb", Helper.TestObbPath,
			"--classdata", Helper.TestClassDataTPKPath,
			"--extract-asset-to", "./TestData/ExtractedAsset",
			"--no-illustration",
			"--no-blur-illustration",
			"--debug"];

		if (File.Exists(Helper.TestAuxObbPath))
		{
			args.AddRange(["--aux-obb", Helper.TestAuxObbPath]);
		}

		Assert.AreEqual(0, CLI.CLI.Main(args.ToArray()));
	}
}
