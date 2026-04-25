namespace PhiInfo.CLI;

/// <summary>
/// Options for extraction. This is used to pass options from the CLI parser to the extractor.
/// </summary>
public class ExtractOptions
{
	/// <summary>
	/// The stream to apk file.
	/// </summary>
	public Stream? ApkFile { get; set; }
	/// <summary>
	/// The stream to obb file. 
	/// Note: this should not be same stream to apk file, because they may read in parallel.
	/// If they share same file, please use <see cref="File.OpenRead(string)"/> to create two separate streams.
	/// </summary>
	public Stream? ObbFile { get; set; }
	/// <summary>
	/// The stream to auxiliary obb file.
	/// </summary>
	public Stream? AuxObbFile { get; set; }
	/// <summary>
	/// The stream to class data file.
	/// </summary>
	public Stream? ClassDataFile { get; set; }

	/// <summary>
	/// Disable illustration extraction. This only includes high resolution illustration.
	/// </summary>
	public bool NoIllustration { get; set; }
	/// <summary>
	/// Disable low resolution illustration extraction. 
	/// </summary>
	public bool NoLowResIllustration { get; set; }
	/// <summary>
	/// Disable blurred illustration extraction.
	/// </summary>
	public bool NoBlurIllustration { get; set; }
	/// <summary>
	/// Disable music extraction.
	/// </summary>
	public bool NoMusic { get; set; }
	/// <summary>
	/// Disable chart extraction. This includes all charts in all difficulties.
	/// </summary>
	public bool NoCharts { get; set; }
}
