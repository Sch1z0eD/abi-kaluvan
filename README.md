<p align="center"><img src="web/docs/logo.png" width="140" alt="Merchant Ear"></p>

<h1 align="center">Merchant Ear</h1>
<p align="center"><b>Hear what is inside</b> — a helper for the Field Merchant in Arena Breakout: Infinite</p>
<p align="center"><b>English</b> · <a href="README.ru.md">Русский</a></p>

The Field Merchant sells closed cargo: you only see its label, price and size. But when you drag a cargo, it plays the sound of the item inside. Merchant Ear listens to that sound, watches the merchant grid and tells you:

- **what the cargo may hold** — a list of items with their chances;
- **the chance of a red item**;
- **whether the purchase pays off** — mean value against the price, the chance to get more, the range;
- **which cargo are the best buys** in this raid.

## Features

- **Recognition by sound.** The drag sound is matched against a library of 38 sound groups (189 items). A sharp-attack check filters out false triggers from wind and music.
- **Cargo under the cursor.** The client reports the cursor position: the cargo you hover is outlined at once, and a sound belongs to the cargo that was under the cursor at the moment of the click.
- **Merchant map.** The grid is found automatically, labels are read by OCR, and a map of the whole list is built while you scroll; the part on screen is outlined. After a purchase the map rebuilds itself, and "Scan again" is instant.
- **Exact odds.** A cargo's pool is picked by its label, price and size: every pool holds items of one size, so items of another size are ruled out. With a discount, the crossed-out price picks the pool.
- **Payback and best buys.** For every cargo with a known price: mean value, chance to pay off, and a verdict — worth buying / a gamble / will not pay off. The best buys are badged on the map (#1, #2…, by your purchases per raid: 2, 3 or 4). Value by the trader hand-in price, the field merchant (+10%) or the market.
- **Purchase log.** The "… spent … Koen … obtained […]" line and your balance confirm your own purchase; it refines the odds for the future.
- **Overlay.** A one-monitor mode: a small window over the game with the map on the left and the result and best buys on the right.
- **Interface** in Ukrainian, English and Russian.

## How it works

| Part | What it does | Where it runs |
|---|---|---|
| [`web/`](web/README.md) | The site: all recognition (grid, text, sound, odds, payback) runs in the browser | Inside `MerchantEar.exe`, or any web server |
| [`client/`](client/README.md) | A C# app (.NET 10): serves the site, sound of the game process only, frames of the game window only, the cursor position — over WebSocket | The gaming PC with Windows |

```
[gaming PC]  MerchantEar.exe ──► http://localhost:8777/  (the site, built in)
                   ▲                       │ page in Chrome or Edge
                   └─────── ws:// ─────────┘
             sound and window of UAGame.exe
```

`MerchantEar.exe` is one file: the site, the client and the .NET runtime inside, nothing to install. The site can also live on its own server; then the exe only streams the sound and picture to it. Without the exe at all the site asks you to share the screen with system audio (Chrome or Edge on Windows); with it the sound is cleaner and the cargo under the cursor is known exactly.

The client does not read or change the game's memory or files: it captures the process sound with the standard Windows mechanism (like OBS and Discord), takes pictures of the game window and reads the system cursor position.

## Download and run

You need Windows 10 (build 20348+) or 11 and Chrome or Edge.

1. Download `MerchantEar.exe` from the [latest release](https://github.com/Sch1z0eD/merchant-ear/releases/latest) and run it. The site opens in the browser at `http://localhost:8777/`; the console window is the client, close it to stop.
2. Switch the game to Borderless window mode and press **Start catching**.
3. Open the Field Merchant and scroll through the list — the map builds itself. Hover a cargo to read its price and drag it — the sound tells what is inside.

Windows SmartScreen may warn about an unknown app: the exe is not signed. "More info" → "Run anyway", or build it yourself from the source.

## Build from source

You need the .NET 10 SDK. Run the client with the site built in:

```bash
cd client
dotnet run -c Release
```

The single `MerchantEar.exe`, as in the releases:

```bash
dotnet publish client -c Release -o publish
```

To work on the site without rebuilding, serve `web/` separately:

1. Start the client:

   ```bash
   cd client
   dotnet run -c Release
   ```

2. In a second window, start the site:

   ```bash
   cd web
   python -m http.server 8080
   ```

3. Open `http://localhost:8080/`.

How to put the site on your own server with HTTPS and a password: [web/README.md](web/README.md). Client settings: [client/README.md](client/README.md).

## Limits

- The odds come from the merchant tables (abibuilder.com / abi-tracker data), not from live statistics. Payback uses the table prices of the items.
- Many items share one sound: inside a sound group the item is told apart only by the pool weights.
- Cargo labels are read only with the English game client. The grid, sound and prices work in any language.
- Borderless window mode is required: in exclusive fullscreen the frame comes out black.

## Disclaimer

This is a fan-made tool, not affiliated with MoreFun Studios or Tencent. The game itself encourages listening to cargo by ear, but the Arena Breakout: Infinite EULA (sections 7(b), 7(c), 7(i)) forbids third-party programs that collect information about the game or give an advantage. Use it at your own risk, do not share the site publicly and do not show it on streams.

## Credits and license

Merchant Ear is licensed under the **GNU GPL v3.0** — the full text is in [LICENSE](LICENSE). You may use, study, change and share it; changed versions must stay under the same license and come with their source code.

- Merchant Ear — © 2026 Sch1z0eD. A modified version: split into a site and a client, with the cursor from the client, a steady connection, noise protection, payback, best buys and much more (see the git history).
- Based on ABI Kaluvan (neverh8me, GPL-3.0) — https://github.com/neverh8me/abi-kaluvan
- [Tesseract.js](https://github.com/naptha/tesseract.js) (Apache-2.0) — text recognition, `web/lib/tesseract`.
- Merchant data and item icons — [abibuilder.com](https://abibuilder.com); item sounds — Item Sonar and user recordings. The game's sounds, icons and data belong to their owners and are not covered by the project license.
