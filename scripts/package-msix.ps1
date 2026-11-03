param(
    [string]$PublishDirectory = "publish",
    [string]$OutputPackage = "store-package/MicroMusicRoom.msix"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishPath = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot $PublishDirectory)).Path
$outputPath = Join-Path $repositoryRoot $OutputPackage
$manifestSource = Join-Path $repositoryRoot "StorePackage\AppxManifest.xml"
$iconSource = Join-Path $repositoryRoot "Assets\musicroom.png"

if (-not (Test-Path -LiteralPath $publishPath -PathType Container)) { throw "The published app folder was not found: $publishPath" }
if (-not (Test-Path -LiteralPath $manifestSource -PathType Leaf)) { throw "The Store package manifest was not found: $manifestSource" }
if (-not (Test-Path -LiteralPath $iconSource -PathType Leaf)) { throw "The app icon was not found: $iconSource" }

$projectPath = Join-Path $repositoryRoot "MusicRoom.csproj"
[xml]$project = Get-Content -Raw -LiteralPath $projectPath
$versionNode = $project.SelectSingleNode("/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='Version']")
if ($null -eq $versionNode -or $versionNode.InnerText -notmatch '^\d+\.\d+\.\d+$') { throw "MusicRoom.csproj must define a three-part numeric Version." }
$packageVersion = "$($versionNode.InnerText).0"

$layout = Join-Path $env:RUNNER_TEMP ("MicroMusicRoom-MSIX-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $layout | Out-Null
Copy-Item -Path (Join-Path $publishPath "*") -Destination $layout -Recurse -Force

$manifestPath = Join-Path $layout "AppxManifest.xml"
Copy-Item -LiteralPath $manifestSource -Destination $manifestPath
[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath
$identity = $manifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
if ($null -eq $identity) { throw "AppxManifest.xml is missing its package identity." }
$identity.SetAttribute("Version", $packageVersion)
$manifest.Save($manifestPath)

$assetsPath = Join-Path $layout "Assets"
New-Item -ItemType Directory -Path $assetsPath -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$sourceImage = [System.Drawing.Image]::FromFile($iconSource)
try {
    foreach ($logo in @(@{ Name = "StoreLogo.png"; Size = 50 }, @{ Name = "Square44x44Logo.png"; Size = 44 }, @{ Name = "Square150x150Logo.png"; Size = 150 })) {
        $bitmap = [System.Drawing.Bitmap]::new($logo.Size, $logo.Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($sourceImage, 0, 0, $logo.Size, $logo.Size)
            $bitmap.Save((Join-Path $assetsPath $logo.Name), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally {
    $sourceImage.Dispose()
}

$sdkBin = Join-Path ([Environment]::GetFolderPath("ProgramFilesX86")) "Windows Kits\10\bin"
$makeAppx = Get-ChildItem -Path $sdkBin -Filter "makeappx.exe" -Recurse -ErrorAction Stop |
    Where-Object { $_.FullName -match "\\x64\\makeappx\.exe$" } |
    Sort-Object -Property FullName -Descending |
    Select-Object -First 1
if (-not $makeAppx) { throw "MakeAppx.exe was not found in the Windows SDK." }

$outputDirectory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& $makeAppx.FullName pack /d $layout /p $outputPath /o /v
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed to build the Microsoft Store package." }
Write-Host "Created Microsoft Store package $outputPath (version $packageVersion)."

