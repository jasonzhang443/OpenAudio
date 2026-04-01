param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PackageName = "OpenAudio-win-x64"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$publishScript = Join-Path $scriptRoot "publish.ps1"
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$stagingDir = Join-Path $releaseRoot $PackageName
$zipPath = Join-Path $releaseRoot "$PackageName.zip"
$readmePath = Join-Path $stagingDir "README-FIRST.txt"

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null

if (Test-Path $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

& $publishScript `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -SingleFile `
    -OutputDir $stagingDir

Get-ChildItem -Path $stagingDir -Filter "*.pdb" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

if (Test-Path (Join-Path $stagingDir "Logs")) {
    Remove-Item -LiteralPath (Join-Path $stagingDir "Logs") -Recurse -Force -ErrorAction SilentlyContinue
}

@"
OpenAudio

How to use:
1. Extract this zip somewhere outside Downloads if you want.
2. Run OpenAudio.exe.
3. If VB-Cable is missing, the app will show the install screen with the official download link.
4. After VB-Cable is installed, pick the app you want to route and click Start.
5. In your game, set microphone to the VB-Cable recording device shown inside the app.

Notes:
- This build is self-contained. No .NET SDK is required.
- VB-Cable is still required and is not bundled in this zip.
- The app does not modify game files or inject DLLs into games.
"@ | Set-Content -Path $readmePath -Encoding ascii

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

Write-Host "Release package created at $zipPath"

