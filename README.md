# PhiInfo
*This is forked from [here](https://github.com/PhigrosLabs/PhiInfo), and 
has been modified for asset extraction for 
[PSLDiscordBot](https://github.com/yt6983138/PSLDiscordBot). Readme has been translated, 
and code have been refactored/trimmed.*

Phigros asset extraction implemented using `AssetsTools.NET`.

## Supported functionalities
### Informations
- [x] Extract song informations
- [x] Extract collections
- [x] Extract avatar informations
- [x] Extract tips
- [x] Extract chapter informations
### Resources
- [x] Extract all song resources (music, cover images, charts)
- [x] Extract all collection resources (cover images)
- [x] Extract all avatar resources (avatar images, name to hash map)
- [x] Extract all chapter resources (cover images)
### Core library
Features:
1. Async api for apis involving I/O operations
2. Development-friendly typed models for extracted informations
### CLI
Features: 
1. Ultra-fast extraction with concurrency (All assets in 22.9 seconds on my PC)
2. Flexible options to only extract what you need
3. Automatic downloading of APK and classdata.tpk from TapTap or URL
4. Support for extracting from OBB and auxiliary OBB files
5. Support for multiple languages (Chinese, English, Japanese, Korean, Traditional Chinese)
6. Support for Phigros_Resource compatible format<sup>[1]</sup>, so you can swap to this easily

<sup>[1]</sup>: Currently only supports: `avatar.txt`, `collection.tsv`, `difficulty.tsv`, `tips.txt`, `info.tsv`, and `tmp.tsv`

Different to the original project, this project extract to local only and does not 
spin up a http server, so there is no API endpoint. Instead, the CLI has the following
options:

| Name | Valid values | Default value | Description |
| --- | --- | --- | --- |
| `--download-apk` | `<url>`, `TAPTAP` | | Download APK from URL or TapTap to the `--apk` path or temp dir. |
| `--download-classdata` | `<url>`, `AUTO` | | Download classdata.tpk from URL or automatically to `--classdata` path or temp dir. |
| `--apk` | `<path>` | | Path to the APK file. |
| `--obb` | `<path>` | | Path to the OBB file. |
| `--aux-obb` | `<path>` | | Path to the auxiliary OBB file. |
| `--classdata` | `<path>` | | Path to the class data TPK file. |
| `--extract-info-to` | `<path>` | | Directory to extract Phigros information. |
| `--extract-asset-to`| `<path>` | | Directory to extract Phigros assets. |
| `--no-illustration` | | `false` | Do not extract high-resolution illustrations. |
| `--no-low-res-illustration`| | `false` | Do not extract low-resolution illustrations. |
| `--no-blur-illustration` | | `false` | Do not extract blurred illustrations. |
| `--no-music` | | `false` | Do not extract music. |
| `--no-charts` | | `false` | Do not extract charts. |
| `--language` | `SimplifiedChinese`, `EnglishUS`, `Japanese`, `Korean`, `TraditionalChinese`, `All` | | Target language for extracting collections and tips. |
| `-?, -h, --help` | | | Show help and usage information. |
| `--version` | | | Show version information. |

More detail can be found in the CLI help message.

## Developers: Get started
AI generated wiki: [![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/yt6983138/PhiInfo)
### Installation
Currently, there is no online nuget package as the original author does not want to
upload to nuget gallery due to the nature of this project being a gray-area one. 

Which means, you can only reference this project by using `git submodule` or grab
one in the releases page.
### Info extraction
Use `InfoExtractor` to extract informations. 

Hint: Many classes have static factory methods, which are much more convenient, 
and we also have helper classes like `PhigrosAssetHelper` to help you prepare resources.

Example code:
```csharp
// Open your Phigros APK, OBB, and classdata.tpk files
// If your apk is from TapTap, supplying arguments such as auxObb or obb
// using just apk stream will work since taptap does not split resources.
//
// Just remember to open two different streams otherwise there might be
// concurrency issues
using Stream apk = File.OpenRead("com.PigeonGames.Phigros.apk");
using Stream obb = File.OpenRead("main.obb");
using Stream classDataTpk = File.OpenRead("classdata.tpk");
  
// Construct the extractor from static factory method,
// please do not construct the extractor multiple times, 
// check the constructor documentation for details.
using InfoExtractor extractor = await InfoExtractor.FromApkAndObbAsync(apk, obb, classDataTpk);  
  
// Check game version and region
Console.WriteLine($"Phigros version: {extractor.GetVersionString()} ({extractor.GetVersionInteger()})");
Console.WriteLine($"International build: {extractor.GetIsInternational()}");
  
// Extract non-language-specific info
List<SongInfo> songs = extractor.ExtractSongInfo(); 
List<Avatar> avatars = extractor.ExtractAvatars();
List<ChapterInfo> chapters = extractor.ExtractChapters();

// Extract language-specific collections and tips
// Note: ExtractLanguage defaults to Language.SimplifiedChinese,
// and this does not support a "All" option (you can only use that with CLI)
extractor.ExtractLanguage = Language.EnglishUS;

List<string> tips = extractor.ExtractTips();
List<Folder> collections = extractor.ExtractCollections(); // requires OBB (level22)
```
### Asset extraction
Use `AssetExtractor` to extract assets.

Hint: As mentioned above, many classes have static factory methods, 
which are much more convenient, and we also have helper classes 
like `PhigrosAssetHelper` to help you prepare resources.

Example code:
```csharp
// Please check the information extraction example for details about
// how to prepare streams.
using Stream obb = File.OpenRead("main.obb");
using Stream auxObb = File.OpenRead("patch.obb"); // optional auxiliary OBB  
  
AddressableBundleExtractor assetExtractor =
    await AddressableBundleExtractor.FromObbAsync(obb, auxObb);
  
// List all available asset paths, this would trim out entries that only contains
// bundle name. Misc entries like shaders or ui assets may still exist.
List<string> paths = assetExtractor.ListMeaningfulAssetPathsInCatalog();
  
// Extract a song illustration as PNG
UnityImage rawImage = await assetExtractor.GetImageRawAsync("Assets/Tracks/Glaciaxion.SunsetRay.0/IllustrationLowRes.jpg");
Image image = rawImage.Decode();
await image.SaveAsync("illustration.png", new PngEncoder());
  
// Extract a song's audio as OGG
var rawMusic = await assetExtractor.GetMusicRawAsync("Assets/Tracks/Glaciaxion.SunsetRay.0/music.wav");
var bank = rawMusic.Decode();
File.WriteAllBytes("music.ogg", bank.ToOggBytes());
  
// Extract a chart file  
var chart = await assetExtractor.GetTextRawAsync("Assets/Tracks/Glaciaxion.SunsetRay.0/Chart_IN.json");
File.WriteAllText("Chart_IN.json", chart.Content);
```
## License
Extracted resources are copyrighted by `南京鸽游网络有限公司` aka `Pigeon Games` and 
their own authors, I do not own any of the resources, and I am not responsible 
for any legal issues caused by using these resources.

If `南京鸽游网络有限公司` aka `Pigeon Games`, or any other copyright holders, have any 
issues with this project, please contact me.

**WARNING: DO NOT use any asset or info extracted by this tool in commercial way. 
This can get both developers and you in trouble.**

### After 2026/03/22
Open sourced under `AGPL-3.0-only` license.
### Before 2026/03/22
Open sourced under `MIT` license.
