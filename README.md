# CS Live Mute

`CS Live Mute` is a Windows-first desktop utility for `CS2` that listens for local `Game State Integration` events and controls browser playback locally on Windows.

## What it does

- Receives `CS2` GSI payloads on `http://127.0.0.1:3000/gsi`
- Switches into combat mode when `round.phase == "live"`
- Lets you choose `Stream` mode or `Video` mode in the app
- `Stream` mode mutes the current browser audio session during live rounds
- `Video` mode pauses the current Windows browser media session during live rounds
- Restores only the audio or playback state that the app changed itself

## Project layout

- `src/CsLiveMute.Core`: GSI parsing, combat-mode state, protocol models
- `src/CsLiveMute.Desktop`: WPF desktop app, local HTTP host, Windows media-session controller, Stitch-inspired UI
- `tests/CsLiveMute.Core.Tests`: unit tests for parsing, config generation and state transitions
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
- Pushes to `main` create a portable `win-x64` zip containing the desktop app and installation guide
