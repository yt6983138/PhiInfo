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

## License
Extracted resources are copyrighted by `南京鸽游网络有限公司` aka `Pigeon Games` and 
their own authors, I do not own any of the resources, and I am not responsible 
for any legal issues caused by using these resources.

If `南京鸽游网络有限公司` aka `Pigeon Games`, or any other copyright holders, have any 
issues with this project, please contact me.

### After 2026/03/22
Open sourced under `AGPL-3.0-only` license.
### Before 2026/03/22
Open sourced under `MIT` license.
