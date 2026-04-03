# Addressable bundles
Almost all resources in the game are stored as Unity addressable bundles instead of 
directly in the game files/directories or game apk.

## Songs
Assets for each song is stored under directory `Assets/Tracks/{SongInfo.Id}/`.
Example: `Assets/Tracks/Aleph0.LeaF.0/` contains the following assets:
1. Music `music.wav`
2. Chart files `Chart_EZ.json`, `Chart_HD.json`, 
    `Chart_IN.json`, `Chart_AT.json`, `Chart_Legacy.json`
3. Illustration files `Illustration.jpg`, `IllustrationLowRes.jpg`, `IllustrationBlur.jpg`

## Avatars
Stored as `{Avatar.AddressablePath}`. This looks a bit abstract, 
so here's an example: `avatar.駡人ol`. This points to avatar image directly.

## Chapter covers
Stored as `Assets/Tracks/#ChapterCover/{ChapterInfo.Code}.jpg`.