namespace PhiInfo.Tests;

[TestClass]
public class CLITest
{
	[TestMethod]
	public void Information()
	{
		Helper.EnsureWorkingDirectory();
		Assert.AreEqual(0, CLI.CLI.Main([
			.. Helper.TestPackages.Where(File.Exists).Select(x => new string[] { "-p", x }).SelectMany(x => x).ToArray(),
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
			.. Helper.TestPackages.Where(File.Exists).Select(x => new string[] { "-p", x + ".tmp" }).SelectMany(x => x).ToArray(),
			"--extract-info-to", "./TestData/ExtractedInfoAuto",
			"--language", "EnglishUS",
			"--debug"]));
	}
	[TestMethod]
	public void Asset()
	{
		Helper.EnsureWorkingDirectory();

		List<string> args = [
			.. Helper.TestPackages.Where(File.Exists).Select(x => new string[] { "-p", x }).SelectMany(x => x).ToArray(),
			"--extract-asset-to", "./TestData/ExtractedAsset",
			"--no-illustration",
			"--no-blur-illustration",
			"--debug"];

		Assert.AreEqual(0, CLI.CLI.Main(args.ToArray()));
	}
}
