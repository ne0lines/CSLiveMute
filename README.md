# CS Live Mute

`CS Live Mute` is a Windows-first desktop utility for `CS2` that listens for local `Game State Integration` events and synchronizes browser playback across YouTube, Twitch and Kick.

## What it does

- Receives `CS2` GSI payloads on `http://127.0.0.1:3000/gsi`
- Switches into combat mode when `round.phase == "live"`
- Broadcasts combat mode over a local WebSocket bridge on `ws://127.0.0.1:3000/bridge`
- Mutes live streams and pauses VOD playback in Chromium-based browsers via the bundled unpacked extension
- Restores only the playback state that the extension changed itself

## Project layout

- `src/CsLiveMute.Core`: GSI parsing, combat-mode state, protocol models
- `src/CsLiveMute.Desktop`: WPF desktop app, local HTTP/WebSocket host, Stitch-inspired UI
- `tests/CsLiveMute.Core.Tests`: unit tests for parsing, config generation and state transitions
- `extension`: unpacked Chromium extension for Chrome, Edge and Brave
- `.github/workflows`: CI and release automation

## Local development

```powershell
dotnet build CsLiveMute.sln
dotnet test CsLiveMute.sln
dotnet run --project src/CsLiveMute.Desktop/CsLiveMute.Desktop.csproj
```

The app generates a token on first launch and stores settings in `%LOCALAPPDATA%\CsLiveMute\settings.json`.

## Release flow

- `dev`: integration branch for normal development
- `main`: release branch
- GitHub Actions builds and tests on `dev` and `main`
- Pushes to `main` create a portable `win-x64` zip containing the desktop app, unpacked extension and installation guide
