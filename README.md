# Micro Music Room

A small dark themed Windows desktop music player. It uses WPF and Windows' built-in media playback, with no third-party package dependencies.

Copyright (C) 2026 Marko Idžan. This project is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3 or (at your option) any later version. It is provided without any warranty. See [LICENSE](LICENSE). The DJ icon is by [Icons8](https://icons8.com) and is credited in the app's About section.

See [Contributors](CONTRIBUTORS.md).

## Run on Windows 11

Install the .NET 8 SDK, open this folder in a terminal, and run:

```powershell
dotnet run
```

To build a standalone Windows 11 executable and ZIP package:

```powershell
dotnet publish MusicRoom.csproj -c Release -r win-x64 --self-contained true -o publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
Compress-Archive -Path "publish\*" -DestinationPath "MicroMusicRoom-win-x64.zip" -CompressionLevel Optimal
pwsh ./scripts/package-msix.ps1 -PublishDirectory "publish" -OutputPackage "store-package/MicroMusicRoom.msix"
```

The published app and license will be in the `publish/` folder. The GitHub archive is `MicroMusicRoom-win-x64.zip`; the Store upload package is `store-package/MicroMusicRoom.msix`.

## GitHub Actions

The **Build Windows package** workflow builds the app and uploads both `MicroMusicRoom-win-x64.zip` and `MicroMusicRoom.msix` on pushes to `main`, pull requests, or manual runs. Download the `MicroMusicRoom-MSIX-Store` workflow artifact and upload its MSIX to the reserved Partner Center product. Microsoft signs Store submissions after certification. To create a GitHub Release with the Windows ZIP attached, push a version tag such as `v1.0.3`.

### Signed releases

GitHub ZIP releases can be signed with an Authenticode code-signing certificate. To enable signing, add these repository **Actions secrets** under **Settings → Secrets and variables → Actions**:

- `WINDOWS_SIGNING_CERTIFICATE_BASE64` — the password-protected PFX certificate, encoded as Base64.
- `WINDOWS_SIGNING_CERTIFICATE_PASSWORD` — the PFX password.

When both secrets are set, the release workflow signs and verifies `MicroMusicRoom.exe` before packaging it. Without them, it still creates the GitHub ZIP, which remains unsigned. Never commit the certificate or its password. The signing certificate must be issued for code signing by a trusted certificate authority; a regular HTTPS/TLS certificate will not work. The Store MSIX is intentionally unsigned; Microsoft signs it after Store certification.

## Music and playlists

Add a folder to import its supported tracks and create or update a playlist named after the folder. Add individual songs to the library or to the currently open playlist. Select songs and use **Add to playlist** to build mixes from different artists. Create, rename and delete playlists from the sidebar and playlist header.

The app saves playlists and file locations in `%LOCALAPPDATA%\MusicRoom\library.json`. Audio stays in its original folder. If files are moved or the library is used on another PC, import the folder again. Supported extensions are MP3, M4A, WAV, AAC, WMA and FLAC. Actual playback depends on the Windows audio codecs available on that PC; FLAC codec availability can vary.

