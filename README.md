# OpenAudio

A Windows desktop app that captures audio from a selected application, optionally mixes in a live microphone, and routes the result into VB-Cable so games can use it as a microphone input.

![OpenAudio screenshot](docs/app-screenshot.png)

## Download

[**Download for Windows x64**](https://github.com/jasonzhang443/OpenAudio/releases/download/v1.0.0/OpenAudio-win-x64.zip)

1. Extract the zip.
2. Run `OpenAudio.exe`.
3. If VB-Cable is not installed, the app will show a setup screen with the official download link.
4. Pick the app you want to route and click **Start**.
5. In your game, set the microphone to the VB-Cable device shown in the app.

> No .NET installation required — the build is self-contained.

## Antivirus Warning

Some antivirus tools may flag `OpenAudio.exe` as suspicious. This is a false positive caused by the self-contained .NET packaging — the entire .NET runtime is bundled into a single compressed executable, which low-quality AV engines sometimes flag as malware because malware uses the same technique.

2 out of ~70 engines on VirusTotal flag it, both with generic AI-based labels and no specific signature:

- **Bkav Pro** — `W64.AIDetectMalware` (AI guess, no real signature)
- **VirIT** — `Trojan.Win64.Agent.JJE` (generic catch-all label)

The source code is fully open so you can verify or build it yourself: [VirusTotal scan](https://www.virustotal.com/gui/file/a88c1f34a7a08b3d2fcc393484474a027e0ed33233f6f56c91543f7d98be5224/detection)

## How It Works

- Detects VB-Cable on launch and blocks usage until it is installed.
- Captures audio from a single app (Spotify, Chrome, Firefox, etc.) using Windows process loopback capture.
- Optionally mixes in a real microphone.
- Resamples and mixes both streams, then outputs to VB-Cable.
- Shows exactly which VB-Cable recording endpoint to select in-game.

## Requirements

- Windows 10 (build 20348+) or Windows 11
- [VB-Cable](https://vb-audio.com/Cable/) — free virtual audio driver

## Building

```powershell
dotnet restore
dotnet build .\OpenAudio.sln
```

Or open `OpenAudio.sln` in Visual Studio 2022 and build from there.

## Publishing

```powershell
.\scripts\publish.ps1
# Output: artifacts\publish\win-x64\
```

To create a release zip:

```powershell
.\scripts\package-release.ps1
# Output: artifacts\release\OpenAudio-win-x64.zip
```

## Releases

Push a version tag to trigger a GitHub Release automatically:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

## Troubleshooting

**VB-Cable not detected** — Re-run the VB-Cable installer as administrator, reboot, then click *I installed it, recheck* in the app.

**No audio in-game** — Make sure the target app is open before clicking Start. Set the game microphone to the VB-Cable recording endpoint shown in the app.

**Microphone too loud/quiet** — Adjust the Mic volume slider in the app.

**App stops after unplugging a device** — Reconnect, reselect the device if needed, and press Start again.

## License

MIT — see [LICENSE](LICENSE).
