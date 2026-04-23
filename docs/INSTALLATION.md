# Installation

## 1. Start the desktop app

Launch `CS Live Mute` and leave it running while you play `CS2`.

## 2. Choose your mode

- Choose `Stream` if you normally watch Twitch, Kick or another live stream.
- Choose `Video` if you normally watch YouTube VOD or another normal video.

## 3. Add the CS2 GSI config

Copy the config block from the app and save it as:

`<your Steam library>\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\gamestate_integration_cs_live_mute.cfg`

If your Steam library is on the default Windows path, the folder is usually:

`C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\`

## 4. Verify the local controller

- Start a browser video or stream before you test.
- The app should show the detected browser media session in `Media Detected`.
- When `CS2` sends a valid payload, `Game Status` changes to connected.

## 5. Usage rules

- `Stream` mode mutes the current browser audio session during live rounds.
- `Video` mode pauses the current browser media session during live rounds.
- Audio or playback is restored only if the desktop app made the original change.
