# spoverlay

A compact Windows desktop overlay with a translucent animated background, still album artwork, playback controls, and a system-tray icon. Uses Windows media sessions, so no Spotify login or API key is needed.

## Run

Download the repository and open `app/SpotifyOverlay.exe`. Keep the other files in `app` beside it. Requires Windows 10/11 and .NET Framework 4.8.

- Click-through hides the playback, minimize, and close buttons. Turning it off restores them.
- Ctrl+Alt+. toggles click-through mode on and off globally. While enabled, mouse clicks pass to windows behind the overlay.
- Click through is also available from the overlay and tray menus. If the shortcut is unavailable, the app keeps this mode disabled.
- Drag the background to move the overlay, including partially beyond the screen edge. Use Reset position in the tray menu to recover it.
- Hover over the overlay to scroll long song titles in a loop.
- Right-click for Transparency: the top slider fades the background; the bottom slider fades artwork, text, and buttons. Both save automatically. Hover a slider for its tooltip. Use the tray menu if the overlay becomes invisible.
- Use previous, play/pause, and next below the song details.
- The overlay stays visible without a taskbar button.
- Minimize hides to the system tray. Double-click the tray icon to restore.
- Close exits the app.
- Right-click for Album cover only and Always on top.

The app reads Spotify playback on this PC. Playback on other devices is not supported. It does not start automatically with Windows.

## Build

Run `build.ps1` from PowerShell. Requires .NET Framework 4.8 and a Windows 10/11 SDK with UnionMetadata. The resulting app runs without PowerShell.

## Validation

Native interface, cover-only mode, GIF decoding, and a Windows artwork-stream check passed. Live Spotify artwork and transport behavior still need verification outside the restricted build environment.

## Artwork

The background GIF was supplied by the project owner as `alexandra-vezhnovets-neon-room.gif`; its visible signature reads milky-horros. It is included unchanged. No ownership or license to third-party artwork is claimed.







