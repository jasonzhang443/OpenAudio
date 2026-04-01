# Architecture

## Overview

OpenAudio is a single WPF application that keeps the MVP focused on reliable app-to-game routing:

1. Detect VB-Cable endpoints
2. Let the user pick a single application as the music source
3. Optionally capture a live microphone
4. Normalize both streams into one float mix format
5. Mix and limit the result
6. Render the mixed stream into the VB-Cable playback endpoint
7. Let the game read the paired VB-Cable recording endpoint as its microphone

## Main Components

### `VbCableDetector`

- Scans active Windows render and capture endpoints
- Locates the VB-Cable render endpoint used for app output
- Locates the VB-Cable capture endpoint the user must choose in the game
- Reports installed status and friendly names

### `AudioDeviceService`

- Enumerates active playback and microphone devices
- Filters VB-Cable out of user-selectable source devices to prevent feedback loops
- Exposes device-change notifications for hot-plug refresh
- Resolves device IDs back to `MMDevice` instances

### `ProcessLoopbackCaptureService`

- Uses `ActivateAudioInterfaceAsync` with `VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK`
- Captures audio for one selected process tree, such as Spotify or a browser
- Buffers captured PCM and exposes it through `IAudioSource`

### `MicrophoneCaptureService`

- Wraps WASAPI capture for a selected microphone endpoint
- Buffers microphone PCM
- Exposes the source through `IAudioSource`

### `AudioMixerService`

- Chooses a sane float mix format based on the VB-Cable render endpoint
- Adapts channel counts
- Resamples sources as needed
- Applies per-source volume
- Mixes both sources
- Applies a simple limiter to prevent hard clipping

### `VirtualCableOutputService`

- Opens the VB-Cable render endpoint in shared mode
- Pulls the mixed stream and plays it into VB-Cable
- Raises runtime faults if playback stops unexpectedly

### `MainViewModel`

- Owns UI state, commands, selections, status text, and routing lifecycle
- Refreshes devices on startup and on audio topology changes
- Refreshes visible application candidates on a timer while idle
- Starts and stops the capture, mix, and output chain in a safe order
- Surfaces user-readable errors

### `SessionLogger`

- Stores log lines for the current session in memory
- Attempts to mirror those lines to a text file inside the app folder

## Audio Flow

```text
Selected application
    -> app process loopback capture
    -> buffered source
    -> format normalization
    -> music gain
                                \
                                 -> mixer -> limiter -> VB-Cable render endpoint
                                /
Optional microphone
    -> WASAPI capture
    -> buffered source
    -> format normalization
    -> mic gain
```

## Format Strategy

- Internal mixing uses IEEE float samples
- Target sample rate follows the VB-Cable render endpoint mix format when available
- Target channel count is kept to mono or stereo for practical compatibility
- Each source is:
  - converted to float
  - channel-aligned
  - resampled if needed

## Reliability Notes

- `BufferedWaveProvider` is used with `ReadFully = true` so silence is produced cleanly when a source is idle.
- Device removal is handled pragmatically:
  - the UI refreshes available devices
  - an active session is stopped if a required device disappears
- Selected devices are released on stop and on app exit.

## Future-Friendly Extension Point

The app keeps one important extension point:

- `IAudioSource`

That interface lets a future audio source implementation, such as per-process capture, plug into the existing mixer and output chain without forcing the UI or mixer to know how the source is captured.

