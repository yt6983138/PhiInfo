using PhiInfo.Core;
using PhiInfo.Core.Models.Information;

namespace PhiInfo.Tests;

[TestClass]
public sealed class ExtractTest
{
	[TestMethod]
	public void TestExtraction()
	{
		Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, "..", "..", "..");

		using PhigrosRawAssetExtractor rawExtractor = PhigrosRawAssetExtractor.FromApkAndObb(
			File.OpenRead("./TestData/base.apk"),
			File.OpenRead("./TestData/obb.obb"),
			File.OpenRead("./TestData/classdata.tpk"));

		PhigrosExtractedDataCollection info = rawExtractor.ExtractAll();
		Console.WriteLine(info);
	}
}
