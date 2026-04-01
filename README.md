# OpenAudio

OpenAudio is a Windows desktop app that captures audio from a selected application, optionally mixes in a live microphone, and routes the result into VB-Cable so games can see it as a microphone.

![OpenAudio screenshot](docs/app-screenshot.png)

## For End Users

If you are downloading this from GitHub, use this packaged app zip:

- [Download OpenAudio for Windows x64](release-assets/OpenAudio-win-x64.zip?raw=1)

Then:

1. Extract the zip.
2. Run `OpenAudio.exe`.
3. If VB-Cable is not installed yet, use the built-in setup screen to install it from the official site.
4. Pick the app you want to route and click `Start`.
5. In your game, set microphone to the VB-Cable device shown in the app.

The release build is self-contained, so the user does not need to install the .NET SDK.

## What It Does

- Detects whether VB-Cable is installed when the app launches.
- Blocks usage with a setup screen until VB-Cable is available.
- Captures a single application such as Spotify or a browser by using Windows process loopback capture.
- Optionally captures a real microphone.
- Lets the user mute and unmute the microphone without stopping the routed music.
- Resamples and aligns both streams to a common format.
- Mixes both sources in-app and outputs the final stream to the VB-Cable render endpoint.
- Tells the user exactly which VB-Cable recording endpoint to select inside the game.

## MVP Scope

- Windows only
- .NET 8
- WPF desktop UI
- NAudio-based device enumeration, capture, buffering, mixing, and playback
- Process-based loopback capture for a single selected app on supported Windows builds

Spotify, YouTube, browser tabs, and local media players can be routed by selecting the specific app process such as Spotify, Chrome, Edge, or Firefox.

## Prerequisites

1. Windows 10 or Windows 11
2. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for local build and publish
3. [VB-Cable from VB-Audio](https://vb-audio.com/Cable/)

## Setup For Users

1. Install VB-Cable from the official VB-Audio site.
2. Reboot if the installer asks for it.
3. Launch OpenAudio.
4. Pick the app you want to route, such as Spotify or your browser.
5. Start audio in that app.
6. Optionally enable and pick a microphone.
7. Click `Start`.
8. In your game's voice chat settings, choose `CABLE Output (VB-Audio Virtual Cable)` or the exact name shown in the app.

## Build

From the repository root:

```powershell
dotnet restore
dotnet build .\OpenAudio.sln
```

## Publish

Use the included script to publish a self-contained Windows x64 build:

```powershell
.\scripts\publish.ps1
```

Default publish output:

```text
artifacts\publish\win-x64\
```

## Package A GitHub Release

To create the GitHub-friendly download package:

```powershell
.\scripts\package-release.ps1
```

This creates:

```text
artifacts\release\OpenAudio-win-x64.zip
```

That zip is intended to be uploaded to a GitHub Release for end users.

## GitHub Automation

This repository includes a GitHub Actions workflow at:

```text
.github\workflows\release.yml
```

What it does:

- builds a Windows x64 release package
- uploads `OpenAudio-win-x64.zip` as a workflow artifact
- if the push is a tag like `v1.0.0`, attaches that zip to a GitHub Release automatically

## Running

If you are using the .NET CLI:

```powershell
dotnet run --project .\src\OpenAudio\OpenAudio.csproj
```

If you are using Visual Studio 2022:

1. Open `OpenAudio.sln`
2. Restore NuGet packages
3. Build and run the `OpenAudio` project

## Troubleshooting

### VB-Cable is not detected

- Re-run the VB-Cable installer as administrator.
- Reboot Windows after install.
- Click `I installed it, recheck`.
- Confirm that Windows shows both:
  - `CABLE Input (VB-Audio Virtual Cable)` under playback devices
  - `CABLE Output (VB-Audio Virtual Cable)` under recording devices

### I cannot hear any music in-game

- If you are in `Application only` mode, make sure the target app is already open when you press `Start`.
- Browser capture works best when you choose the browser process itself.
- Make sure the game microphone is set to the VB-Cable recording endpoint shown in the app.
- Do not select VB-Cable as the music source or microphone source.

### My microphone is too loud or too quiet

- Adjust `Mic volume` in the app.
- If the source is clipping, lower both the music and mic sliders for more headroom.

### The app stops after unplugging a device

- This is expected behavior for the MVP.
- Reconnect the device, reselect it if needed, and press `Start` again.

## Logs

The app keeps an in-memory session log and also attempts to write a text log under:

```text
<app folder>\Logs\
```

If the app does not have permission to write there, it will continue running with in-memory logging only.

## Limitations

- No recording to files
- No game-specific integrations
- No system tray behavior
- No automatic device switching while already running
- Application-only capture requires Windows 10 build 20348 or later
- VB-Cable is still required and is not bundled with the app

## Future Extension

The capture pipeline already uses an `IAudioSource` interface so more app-selection and filtering options can be added later without rewriting the mixer or output layers.

