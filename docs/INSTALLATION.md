# Installation

## 1. Start the desktop app

Launch `CS Live Mute` and leave it running while you play `CS2`.

## 2. Install the Chromium extension

1. Open `chrome://extensions`, `edge://extensions` or `brave://extensions`.
2. Enable `Developer mode`.
3. Choose `Load unpacked`.
4. Select the repository's `extension` folder, or the packaged `extension` folder from a release zip.

## 3. Add the CS2 GSI config

Copy the config block from the app and save it as:

`<your Steam library>\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\gamestate_integration_cs_live_mute.cfg`

If your Steam library is on the default Windows path, the folder is usually:

`C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\`

## 4. Verify the local bridge

- The app should show `Waiting for Chromium extension` until the extension connects.
- Once the extension is loaded, the app should show the connected browser and supported tab count.
- When `CS2` sends a valid payload, `Game Status` changes to connected.

## 5. Usage rules

- `Twitch`, `Kick` and `YouTube Live` are treated as live streams and muted during live rounds.
- `YouTube` VOD playback is paused during live rounds.
- Playback is restored only if the extension made the original change.
