using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using Fmod5Sharp;
using Fmod5Sharp.CodecRebuilders;
using Fmod5Sharp.FmodTypes;
using global.PhiInfo.HttpServer.Type;
using PhiInfo.Core;
using PhiInfo.Core.Models.Information;
using PhiInfo.Core.Models.RawAsset;
using Shua.Zip;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace PhiInfo;

[JsonSerializable(typeof(List<SongInfo>))]
[JsonSerializable(typeof(List<Folder>))]
[JsonSerializable(typeof(List<Avatar>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<ChapterInfo>))]
[JsonSerializable(typeof(PhigrosExtractedDataCollection))]
[JsonSerializable(typeof(ServerInfo))]
public partial class JsonContext : JsonSerializerContext
{
}

public class HttpServer : IDisposable
{
	private readonly JsonContext _jsonContext = new(new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
	});

	private readonly AddressableBundleExtractor _phiAsset;
	private readonly Core.PhigrosRawAssetExtractor _phiInfo;
	protected readonly ShuaZip Zip;
	private HttpListener? _listener;
	private CancellationTokenSource? _cts;
	private bool _disposed;

	private readonly Dictionary<string, Func<HttpListenerRequest, Task<(byte[] data, string contentType)>>>
		_routeHandlers;

	public HttpServer(string apkPath, Stream cldbStream)
	{
		MmapReadAt reader = new(apkPath);
		this.Zip = new ShuaZip(reader);

		using Stream catalogStream = this.Zip.OpenFileStreamByName("assets/aa/catalog.json");
		CatalogParser catalogParser = new(catalogStream);

		this._phiAsset = new PhigrosAssetExtractor(catalogParser,
				(bundleName) => { return this.Zip.OpenFileStreamByName("assets/aa/Android/" + bundleName); });

		this._phiInfo = new Core.PhigrosRawAssetExtractor(
			this.Zip.OpenFileStreamByName("assets/bin/Data/globalgamemanagers.assets"),
			this.Zip.OpenFileStreamByName("assets/bin/Data/level0"),
			this.SetupLevel22(this.Zip),
			this.Zip.ReadFileByName("lib/arm64-v8a/libil2cpp.so"),
			this.Zip.ReadFileByName("assets/bin/Data/Managed/Metadata/global-metadata.dat"),
				cldbStream
			);

		const string jsonStream = "application/json";

		this._routeHandlers = new()
		{
			["/asset/text"] = async r => (this.GetAssetText(r.QueryString["path"]), "text/plain"),
			["/asset/music"] = async r => (this.GetAssetMusic(r.QueryString["path"]), "audio/ogg"),
			["/asset/image"] = async r => (this.GetAssetImage(r.QueryString["path"]), "image/bmp"),
			["/asset/list"] = async _ => (SerializeJson(this._phiAsset.ListAllAssetPathsInCatalog(), this._jsonContext.ListString), jsonStream),
			["/info/songs"] = async _ =>
				(SerializeJson(this._phiInfo.ExtractSongInfo(), this._jsonContext.ListSongInfo), jsonStream),
			["/info/collection"] = async _ =>
				(SerializeJson(this._phiInfo.ExtractCollection(), this._jsonContext.ListFolder), jsonStream),
			["/info/avatars"] = async _ =>
				(SerializeJson(this._phiInfo.ExtractAvatars(), this._jsonContext.ListAvatar), jsonStream),
			["/info/tips"] =
					async _ => (SerializeJson(this._phiInfo.ExtractTips(), this._jsonContext.ListString), jsonStream),
			["/info/chapters"] = async _ =>
				(SerializeJson(this._phiInfo.ExtractChapters(), this._jsonContext.ListChapterInfo), jsonStream),
			["/info/all"] = async _ => (SerializeJson(this._phiInfo.ExtractAll(), this._jsonContext.AllInfo), jsonStream),
			["/info/version"] = async _ =>
				(Encoding.UTF8.GetBytes(Core.PhigrosRawAssetExtractor.GetPhiVersion().ToString()), "text/plain"),
			["/info/server"] = async _ => (SerializeJson(this.GetServerInfo(), this._jsonContext.ServerInfo), jsonStream),
		};
	}

	private async Task HandleRequest(HttpListenerContext context)
	{
		using HttpListenerResponse response = context.Response;
		try
		{
			string path = context.Request.Url?.AbsolutePath.ToLower() ?? "";

			response.Headers.Add("Access-Control-Allow-Origin", "*");

			if (this._routeHandlers.TryGetValue(path, out Func<HttpListenerRequest, Task<(byte[] data, string contentType)>>? handler))
			{
				(byte[] data, string contentType) = await handler(context.Request);
				response.ContentType = contentType;
				response.ContentLength64 = data.Length;
				await response.OutputStream.WriteAsync(data);
			}
			else
			{
				response.StatusCode = (int)HttpStatusCode.NotFound;
				byte[] msg = Encoding.UTF8.GetBytes("Endpoint not found");
				await response.OutputStream.WriteAsync(msg);
			}
		}
		catch (Exception ex)
		{
			response.StatusCode = (int)HttpStatusCode.InternalServerError;
			byte[] errorBuffer = Encoding.UTF8.GetBytes($"Server Error: {ex.Message}");
			await response.OutputStream.WriteAsync(errorBuffer);
		}
	}

	private static byte[] SerializeJson<T>(T data, JsonTypeInfo<T> typeInfo)
	{
		return JsonSerializer.SerializeToUtf8Bytes(data, typeInfo);
	}

	private byte[] GetAssetText(string? path)
	{
		if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is empty");
		UnityText textData = this._phiAsset.GetTextRaw(path);
		return Encoding.UTF8.GetBytes(textData.Content);
	}

	private byte[] GetAssetMusic(string? path)
	{
		if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is empty");
		UnityMusic raw = this._phiAsset.GetMusicRaw(path);
		FmodSoundBank bank = FsbLoader.LoadFsbFromByteArray(raw.Data);
		byte[] music = FmodVorbisRebuilder.RebuildOggFile(bank.Samples[0]);
		return music;
	}

	private static ImageSharpImage LoadEtc(
		ReadOnlySpan<byte> input,
		int width,
		int height,
		bool hasAlpha)
	{
		if (hasAlpha)
		{
			EtcDecoder.DecompressETC2A8<ColorBGRA<byte>, byte>(input, width, height, out byte[]? data);
			return ImageSharpImage.LoadPixelData<Bgra32>(data, width, height);
		}
		else
		{
			EtcDecoder.DecompressETC<ColorBGRA<byte>, byte>(input, width, height, out byte[]? data);
			return ImageSharpImage.LoadPixelData<Bgra32>(data, width, height);
		}
	}

	private byte[] GetAssetImage(string? path)
	{
		if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is empty");
		UnityImage raw = this._phiAsset.GetImageRaw(path);
		using ImageSharpImage img = raw.Format switch
		{
			3 => ImageSharpImage.LoadPixelData<Rgb24>(
				raw.Data,
				(int)raw.Width,
				(int)raw.Height),

			4 => ImageSharpImage.LoadPixelData<Rgba32>(
				raw.Data,
				(int)raw.Width,
				(int)raw.Height),

			34 => LoadEtc(raw.Data, (int)raw.Width, (int)raw.Height, false),

			47 => LoadEtc(raw.Data, (int)raw.Width, (int)raw.Height, true),

			_ => throw new NotSupportedException($"Unknown format: {raw.Format}")
		};

		img.Mutate(x => x.Flip(FlipMode.Vertical));
		using MemoryStream ms = new();
		img.Save(ms, new BmpEncoder());
		return ms.ToArray();
	}

	protected virtual void Log(string msg)
	{
		Console.WriteLine(msg);
	}

	protected virtual AppInfo GetAppInfo()
	{
		return new AppInfo("Unknown", "Unknown");
	}

	private ServerInfo GetServerInfo()
	{
		string rid = RuntimeInformation.RuntimeIdentifier;
		string version = typeof(HttpServer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
				?.InformationalVersion ?? "Unknown";
		AppInfo appInfo = this.GetAppInfo();
		return new ServerInfo(version, rid, appInfo);
	}

	private Stream SetupLevel22(ShuaZip zip)
	{
		List<(int index, string name)> level22Parts = [];

		foreach (FileEntry entry in zip.FileEntries)
		{
			if (entry.Name.StartsWith("assets/bin/Data/level22.split", StringComparison.Ordinal))
			{
				string suffix = entry.Name["assets/bin/Data/level22.split".Length..];
				if (int.TryParse(suffix, out int index))
					level22Parts.Add((index, entry.Name));
			}
		}

		if (level22Parts.Count == 0)
			throw new FileNotFoundException("Required Unity assets missing from APK");

		level22Parts.Sort((a, b) => a.index.CompareTo(b.index));
		MemoryStream level22 = new();
		foreach ((int index, string name) in level22Parts)
		{
			byte[] data = zip.ReadFileByName(name);
			level22.Write(data, 0, data.Length);
		}

		level22.Position = 0;

		return level22;
	}

	public async Task Start(uint port = 41669, string host = "localhost")
	{
		if (this._listener?.IsListening == true) return;

		this._listener = new HttpListener();
		this._listener.Prefixes.Add($"http://{host}:{port}/");

		this._cts = new CancellationTokenSource();
		this._listener.Start();
		await Task.Run(() => this.ListenLoop(this._cts.Token));
	}

	private async Task ListenLoop(CancellationToken token)
	{
		while (!token.IsCancellationRequested && this._listener?.IsListening == true)
		{
			try
			{
				HttpListenerContext context = await this._listener.GetContextAsync();
				_ = Task.Run(() => this.HandleRequest(context), token);
			}
			catch (HttpListenerException)
			{
				break;
			}
			catch (ObjectDisposedException)
			{
				break;
			}
			catch (Exception ex)
			{
				this.Log($"[HttpServer] Accept Error: {ex.Message}");
			}
		}
	}

	public void Stop()
	{
		this._cts?.Cancel();
		if (this._listener?.IsListening == true)
		{
			this._listener.Stop();
			this._listener.Close();
		}
	}

	public void Dispose()
	{
		if (this._disposed) return;
		this.Stop();
		this._cts?.Dispose();
		this.Zip.Dispose();
		this._phiInfo.Dispose();
		this._disposed = true;
		GC.SuppressFinalize(this);
	}
}