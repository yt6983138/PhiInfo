using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace PhiInfo.CLI;

/// <summary>
/// TapTap related downloader, used to get the latest apk download url of a TapTap app.
/// Please do not abuse this class since it is reverse engineered.
/// </summary>
public class TapTapDownloader
{
	/// <summary>
	/// Request the latest apk download url of a TapTap app.
	/// </summary>
	/// <param name="client">The HttpClient instance used for making requests.</param>
	/// <param name="appId">The ID of the TapTap app. Default is Phigros (165287).</param>
	/// <returns>The latest apk download url of the TapTap app.</returns>
	public static async Task<string> GetApkLatestUrlAsync(HttpClient client, int appId = 165287)
	{
		Guid guid = Guid.NewGuid();
		string xUA = $"V=1&PN=TapTap&VN=2.40.1-rel.100000&VN_CODE=240011000&LOC=CN&LANG=zh_CN&CH=default&UID={guid}&NT=1&SR=1080x2030&DEB=Xiaomi&DEM=Redmi+Note+5&OSV=9";

		HttpRequestMessage detailV2Request = new(
			HttpMethod.Get,
			$"https://api.taptapdada.com/app/v2/detail-by-id/{appId}?X-UA={WebUtility.UrlEncode(xUA)}")
		{
			Headers =
			{
				{ "User-Agent", "okhttp/3.12.1" }
			}
		};
		JsonNode detailV2Response = (await JsonNode.ParseAsync(await (await client.SendAsync(detailV2Request)).Content.ReadAsStreamAsync()))
			.EnsureNotNull();

		int apkId = detailV2Response["data"]
			.EnsureNotNull()["download"]
			.EnsureNotNull()["apk_id"]
			.EnsureNotNull()
			.GetValue<int>();

		const string NonceChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
		Span<char> nonce = stackalloc char[NonceChars.Length];
		NonceChars.CopyTo(nonce);
		Random.Shared.Shuffle(nonce);
		nonce = nonce[..5];
		int time = (int)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

		string paramStr = $"abi=arm64-v8a,armeabi-v7a,armeabi&id={apkId}&node={guid}&nonce={nonce}&sandbox=1&screen_densities=xhdpi&time={time}";
		string signString = $"X-UA={xUA}&{paramStr}PeCkE6Fu0B10Vm9BKfPfANwCUAn5POcs";
		string signMD5 = MD5.HashData(Encoding.UTF8.GetBytes(signString)).ToHexString();

		HttpRequestMessage detailV1Request = new(
			HttpMethod.Post,
			$"https://api.taptapdada.com/apk/v1/detail?X-UA={WebUtility.UrlEncode(xUA)}")
		{
			Content = new FormUrlEncodedContent([
				new("node", guid.ToString()),
				new("sandbox", "1"),
				new("sign", signMD5),
				new("abi", "arm64-v8a,armeabi-v7a,armeabi"),
				new("time", time.ToString()),
				new("id", apkId.ToString()),
				new("nonce", new string(nonce)),
				new("screen_densities", "xhdpi"),
			]),
			Headers =
			{
				{ "User-Agent", "okhttp/3.12.1" }
			}
		};
		JsonNode detailV1Response = (await JsonNode.ParseAsync(await (await client.SendAsync(detailV1Request)).Content.ReadAsStreamAsync()))
			.EnsureNotNull();

		return detailV1Response["data"]
			.EnsureNotNull()["apk"]
			.EnsureNotNull()["download"]
			.EnsureNotNull()
			.GetValue<string>();
	}
}
