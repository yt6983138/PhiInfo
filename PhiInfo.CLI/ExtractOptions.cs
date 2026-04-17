namespace PhiInfo.CLI;
public class ExtractOptions
{

	public Stream? ApkFile { get; set; }
	public Stream? ObbFile { get; set; }
	public Stream? AuxObbFile { get; set; }
	public Stream? ClassDataFile { get; set; }

	public bool NoIllustration { get; set; }
	public bool NoLowResIllustration { get; set; }
	public bool NoBlurIllustration { get; set; }
	public bool NoMusic { get; set; }
	public bool NoCharts { get; set; }
}
