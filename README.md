# Music Room

A small dark themed Windows desktop music player. It uses WPF and Windows' built-in media playback, with no third-party package dependencies.

Copyright (C) 2026 Marko Idžan. This project is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3 or (at your option) any later version. It is provided without any warranty. See [LICENSE](LICENSE).

## Run on Windows 11

Install the .NET 8 SDK, open this folder in a terminal, and run:

```powershell
dotnet run
```

To build a standalone Windows 11 executable and ZIP package:

```powershell
dotnet publish MusicRoom.csproj -c Release -r win-x64 --self-contained true -o publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
Compress-Archive -Path "publish\*" -DestinationPath "MusicRoom-win-x64.zip" -CompressionLevel Optimal
```

The published app and license will be in the `publish/` folder; the distributable archive is `MusicRoom-win-x64.zip`.

## GitHub Actions

The **Build Windows package** workflow builds the app and uploads `MusicRoom-win-x64.zip` on pushes to `main`, pull requests, or manual runs. To create a GitHub Release with the packaged app attached, push a version tag such as `v1.0.0`.

## Music and playlists

Add a folder to import its supported tracks and create or update a playlist named after the folder. Add individual songs to the library or to the currently open playlist. Select songs and use **Add to playlist** to build mixes from different artists. Create, rename and delete playlists from the sidebar and playlist header.

The app saves playlists and file locations in `%LOCALAPPDATA%\MusicRoom\library.json`. Audio stays in its original folder. If files are moved or the library is used on another PC, import the folder again. Supported extensions are MP3, M4A, WAV, AAC, WMA and FLAC. Actual playback depends on the Windows audio codecs available on that PC; FLAC codec availability can vary.

