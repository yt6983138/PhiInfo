using Fmod5Sharp.FmodTypes;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using PhiInfo.Core.Extraction;
using PhiInfo.Core.Models.Information;
using PhiInfo.Core.Models.RawAsset;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Security.Cryptography;

namespace PhiInfo.CLI;

/// <summary>
/// File handler for extracted files, the path is the original path in the addressable bundle.
/// The stream is seekable and contains the raw data of the extracted file, you can read from it or save it directly. 
/// You do not need to dispose the stream, it will be disposed by the caller after the handler returns.
/// </summary>
/// <param name="filePath">The original path of the extracted file in the addressable bundle.</param>
/// <param name="data">The stream containing the raw data of the extracted file.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task ExtractedFileHandler(string filePath, Stream data);
/// <summary>
/// A context class for asset extraction, it contains the necessary dependencies and state for extracting assets from the addressable bundle.
/// Note: This class is designed to be single use only.
/// </summary>
public class AssetExtractionContext
{
	/// <summary>
	/// The base path for extracted avatars, the actual file name will be the SHA1 hash of the avatar image data, and the extension will be .png.
	/// </summary>
	public const string AvatarBasePath = "Assets/Avatar/";

	private static readonly PngEncoder _sharedPngEncoder = new();
	private static readonly RecyclableMemoryStreamManager _memoryStreamManager = new();

	private readonly AddressableBundleExtractor _addressableBundleExtractor;
	private readonly ILogger<CLIExtractor> _logger;
	private readonly ExtractOptions _extractOptions;
	private readonly ExtractedFileHandler _extractedFileHandler;
	private readonly FrozenDictionary<string, string> _avatarReverseLookupMap;

	/// <summary>
	/// Flag for whether all assets have been queued for extraction. 
	/// This is used to signal the dequeueing task to stop when there are no more tasks to process.
	/// </summary>
	public volatile bool HasQueuedAll = false;
	/// <summary>
	/// The processing queue for asset extraction tasks.
	/// You may enqueue tasks yourself if you want implement your own extraction logic.
	/// If doing so make sure to set <see cref="HasQueuedAll"/> to true when you are done enqueueing tasks,
	/// and run <see cref="StartDequeue"/> to start the dequeueing process, otherwise the tasks in the queue will not be processed.
	/// </summary>
	public ConcurrentQueue<Task> ProcessingQueue { get; } = new();
	/// <summary>
	/// The map for avatar name to avatar hash, the hash is the SHA1 hash of the avatar image data.
	/// This will only be complete after extraction is done.
	/// </summary>
	public Dictionary<string, string> AvatarMap { get; set; } = [];

	/// <summary>
	/// Constructor for <see cref="AssetExtractionContext"/>.
	/// </summary>
	/// <param name="addressableBundleExtractor">The addressable bundle extractor used for extracting assets.</param>
	/// <param name="preExtractedAvatars">A list of pre-extracted avatars, if any.</param>
	/// <param name="logger">The logger instance for logging extraction progress and errors.</param>
	/// <param name="extractOptions">The options for extraction.</param>
	/// <param name="extractedFileHandler">The handler for processing extracted files.</param>
	public AssetExtractionContext(
		AddressableBundleExtractor addressableBundleExtractor,
		List<Avatar>? preExtractedAvatars,
		ILogger<CLIExtractor> logger,
		ExtractOptions extractOptions,
		ExtractedFileHandler extractedFileHandler)
	{
		this._addressableBundleExtractor = addressableBundleExtractor;
		this._logger = logger;
		this._extractOptions = extractOptions;
		this._extractedFileHandler = extractedFileHandler;

		if (preExtractedAvatars is not null)
		{
			this._avatarReverseLookupMap = preExtractedAvatars.ToFrozenDictionary(x => x.AddressablePath, x => x.Name);
		}
		else
		{
			this._avatarReverseLookupMap = FrozenDictionary<string, string>.Empty;
		}
	}

	private async Task Dequeue()
	{
		while (!this.HasQueuedAll || !this.ProcessingQueue.IsEmpty)
		{
			if (this.ProcessingQueue.TryDequeue(out Task? task))
			{
				await task.ContinueWith(t =>
				{
					if (t.IsFaulted && t.Exception is not null)
					{
						this._logger.LogError(t.Exception, "Error in asset extraction task.");
					}
				});
			}
			else
			{
				await Task.Delay(10);
			}
		}
	}
	/// <summary>
	/// Start the dequeueing process for the processing queue, this will run until <see cref="HasQueuedAll"/> is true and the queue is empty.
	/// </summary>
	/// <returns>A task that represents the asynchronous dequeueing operation.</returns>
	public Task StartDequeue()
	{
		return Task.Run(this.Dequeue);
	}

	/// <summary>
	/// Auto extract all assets concurrently, you do not need to call other extract methods or <see cref="StartDequeue"/> if you call this method.
	/// This method is meant to be a single use method that will extract all assets in the addressable bundle with option specified in the constructor,
	/// which means multiple calls to this may throw an exception (you can manually reset the state but it's more convenient to just new another context)
	/// 
	/// Only call other methods if you want to have more control or invent your own extraction logic.
	/// If doing so, please check <see cref="ProcessingQueue"/> and <see cref="HasQueuedAll"/> for more details.
	/// </summary>
	/// <returns>A task that represents the asynchronous extraction operation.</returns>
	/// <exception cref="InvalidOperationException">Thrown if queueing has already started or ended.</exception>
	public async Task ExtractAll()
	{
		if (this.HasQueuedAll)
		{
			throw new InvalidOperationException("Queueing has already started or ended. Multiple call to ExtractAll is not allowed.");
		}

		Task dequeueTask = this.StartDequeue();
		foreach (string item in this._addressableBundleExtractor.ListMeaningfulAssetPathsInCatalog())
		{
			this.ProcessingQueue.Enqueue(Task.Run(() => this.ExtractAsset(item)));
		}
		this.HasQueuedAll = true;
		await dequeueTask;
	}
	/// <summary>
	/// Extract a single asset by its path in the addressable bundle, the path is the original path of the asset in the bundle, you can get it from the catalog.
	/// This uses options specified in the constructor to determine whether to extract the asset or skip it.
	/// </summary>
	/// <param name="path">Path in the addressable bundle to extract.</param>
	/// <returns>A task that represents the asynchronous extraction operation.</returns>
	public Task ExtractAsset(string path)
	{
		if (path.StartsWith("avatar"))
		{
			return this.ExtractAvatar(path);
		}
		if (!path.StartsWith("Assets/"))
		{
			this._logger.LogInformation("Not a general asset, skipping: {path}", path);
			return Task.CompletedTask;
		}
		if (path.EndsWith(".wav") && !this._extractOptions.NoMusic)
		{
			return this.ExtractMusic(path);
		}
		if (path.EndsWith(".json") && !this._extractOptions.NoCharts)
		{
			return this.ExtractText(path);
		}
		if ((path.Contains("/Illustration.") && this._extractOptions.NoIllustration)
				|| (path.Contains("/IllustrationLowRes.") && this._extractOptions.NoLowResIllustration)
				|| (path.Contains("/IllustrationBlur.") && this._extractOptions.NoBlurIllustration))
		{
			this._logger.LogInformation("Skipping illustration due to options: {path}", path);
			return Task.CompletedTask;
		}
		if (path.EndsWith(".jpg") || path.EndsWith(".png"))
		{
			return this.ExtractIllustration(path);
		}

		this._logger.LogWarning("Unhandled asset type, skipping: {path}", path);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Extract music from specified path.
	/// </summary>
	/// <param name="path">Path in the addressable bundle to extract.</param>
	/// <returns>A task that represents the asynchronous extraction operation.</returns>
	public async Task ExtractMusic(string path)
	{
		this._logger.LogInformation("Extracting music: {path}", path);
		UnityMusic music = await this._addressableBundleExtractor.GetMusicRawAsync(path);
		FmodSoundBank bank = music.Decode();
		using RecyclableMemoryStream stream = _memoryStreamManager.GetStream(path, bank.ToOggBytes());
		stream.Position = 0;
		await this._extractedFileHandler.Invoke(path, stream);
		this._logger.LogInformation("Finished extracting music: {path}", path);
	}
	/// <summary>
	/// Extract illustration (any kind) from specified path.
	/// </summary>
	/// <param name="path">Path in the addressable bundle to extract.</param>
	/// <returns>A task that represents the asynchronous extraction operation.</returns>
	public async Task ExtractIllustration(string path)
	{
		this._logger.LogInformation("Extracting image: {path}", path);
		UnityImage image = await this._addressableBundleExtractor.GetImageRawAsync(path);
		using Image decoded = image.Decode();

		using RecyclableMemoryStream stream = _memoryStreamManager.GetStream(path);
		await decoded.SaveAsync(stream, _sharedPngEncoder);
		stream.Position = 0;

		await this._extractedFileHandler.Invoke($"{path[..^4]}.png", stream);
		this._logger.LogInformation("Finished extracting image: {path}", path);
	}
	/// <summary>
	/// Extract avatar from specified path.
	/// </summary>
	/// <param name="path">Path in the addressable bundle to extract.</param>
	/// <returns>A task that represents the asynchronous extraction operation.</returns>
	public async Task ExtractAvatar(string path)
	{
		this._logger.LogInformation("Extracting avatar: {path}", path);
		UnityImage image = await this._addressableBundleExtractor.GetImageRawAsync(path);
		using Image decoded = image.Decode();
		using RecyclableMemoryStream stream = _memoryStreamManager.GetStream(path);
		await decoded.SaveAsync(stream, _sharedPngEncoder);
		stream.Position = 0;

		string hash = SHA1.HashData(stream).ToHexString();
		stream.Position = 0;

		if (this._avatarReverseLookupMap.TryGetValue(path, out string? name))
		{
			this.AvatarMap[name] = hash;
		}
		else
		{
			this._logger.LogWarning("Not building avatar map for: {path}", path);
		}

		await this._extractedFileHandler.Invoke($"{AvatarBasePath}{hash}.png", stream);
		this._logger.LogInformation("Finished extracting avatar: {path}", path);
	}
	/// <summary>
	/// Extract text (like charts) from specified path.
	/// </summary>
	/// <param name="path">Path in the addressable bundle to extract.</param>
	/// <returns>A task that represents the asynchronous extraction operation.</returns>
	public async Task ExtractText(string path)
	{
		this._logger.LogInformation("Extracting text: {path}", path);
		UnityText text = await this._addressableBundleExtractor.GetTextRawAsync(path);

		using Stream stream = _memoryStreamManager.GetStream(path);
		using StreamWriter streamWriter = new(stream, CLI.UTF8WithoutBOM, leaveOpen: true);
		{
			await streamWriter.WriteAsync(text.Content);
		}
		stream.Position = 0;

		await this._extractedFileHandler.Invoke(path, streamWriter.BaseStream);
		this._logger.LogInformation("Finished extracting text: {path}", path);
	}
}
