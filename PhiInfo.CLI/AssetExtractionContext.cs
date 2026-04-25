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

public delegate Task ExtractedFileHandler(string filePath, Stream data);
/// <summary>
/// Note: single extract all only
/// </summary>
public class AssetExtractionContext
{
	public const string AvatarBasePath = "Assets/Avatar/";
	private static readonly PngEncoder _sharedPngEncoder = new();
	private static readonly RecyclableMemoryStreamManager _memoryStreamManager = new();


	private readonly AddressableBundleExtractor _addressableBundleExtractor;
	private readonly ILogger<CLIExtractor> _logger;
	private readonly ExtractOptions _extractOptions;
	private readonly ExtractedFileHandler _extractedFileHandler;
	private readonly FrozenDictionary<string, string> _avatarReverseLookupMap;

	public volatile bool HasQueuedAll = false;
	public ConcurrentQueue<Task> ProcessingQueue { get; } = new();
	public Dictionary<string, string> AvatarMap { get; set; } = [];

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
	public Task StartDequeue()
	{
		return Task.Run(this.Dequeue);
	}

	/// <summary>
	/// Auto extract all assets concurrently, you do not need to call other extract methods or StartDequeue if you call this method.
	/// 
	/// Only call other methods if you want to have more control or invent your own extraction logic.
	/// </summary>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException"></exception>
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
