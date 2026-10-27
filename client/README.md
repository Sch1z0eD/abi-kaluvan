# Merchant Ear Client (`client/`)

**English** · [Русский](README.ru.md)

A Windows console app on .NET 10. It captures the sound of the game process only, frames of the game window only and the cursor position, and streams them to the site over WebSocket. Every page has its own queue: a slow tab never holds up the capture or the other pages. It serves no site files.

| File | What it does |
|---|---|
| `src/Capture.cs` | Process sound capture through WASAPI process loopback (like OBS and Discord), pictures of the game window through GDI, the cursor position. The game's memory is not read, the process is not opened |
| `src/Server.cs` | `ws://localhost:8777/audio` (48 kHz mono float), `ws://localhost:8777/screen` (JPEG of the right half of the window, up to 30 fps, plus the cursor), `GET /status` |
| `src/Peer.cs` | One connected page: its own sending thread and queue |
| `src/Settings.cs` | Settings and the check which sites may connect |
| `appsettings.json` | Port, game process name, allowed sites |

## Requirements

- Windows 11 or Windows 10 build 20348 or newer (per-process sound capture is needed).
- The .NET 10 SDK to build; the .NET Desktop Runtime 10 to run a ready `.exe`.
- The game in Borderless window mode. In fullscreen the frame comes out black.

## Build and run

```bash
dotnet run -c Release
```

A ready `.exe` into the `publish/` folder:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

## Settings

`appsettings.json` next to the `.exe`:

```json
{
  "port": 8777,
  "exe": "UAGame",
  "allowedOrigins": ["https://abi.example.com"]
}
```

- Pages on `localhost` and `127.0.0.1` are always allowed. Any other site gets a 403, shown in the client window.
- The same options work on the command line: `--port 8799 --exe UAGame --origin https://abi.example.com`.
- If you change the port, change `clientUrl` in `web/config.js` too.

Check that the client is running:

```bash
curl http://localhost:8777/status
```
