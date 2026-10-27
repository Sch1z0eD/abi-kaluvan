# Merchant Ear — site (`web/`)

**English** · [Русский](README.ru.md)

A static site with no build step and no server side. All the work runs in the player's browser:

| File | What it does |
|---|---|
| `index.html` | Interface, grid map, odds, payback, purchase log |
| `config.js` | **Deployment settings:** the client address and an optional online purchase base |
| `migrate.js` | Moves settings saved under earlier names to the current ones, once |
| `vision.js` | Finds the merchant grid and the cargo on a frame |
| `ocr.js`, `lib/tesseract/` | Reads cargo labels and prices (Tesseract.js 5.1.1, works offline) |
| `audio.js` | Catches the drag sound and matches it against the samples |
| `library.js`, `library/` | Sound groups, merchant pools with weights, icons, sound samples |
| `i18n.js` | Texts in Ukrainian, English and Russian |

## Where the site gets sound and picture

1. It first tries the client at `clientUrl` from `config.js` (`ws://localhost:8777` by default). For one visit the address can be overridden: `?client=ws://localhost:8799`.
2. Without the client it calls `getDisplayMedia`, and the browser asks you to share the screen with audio.

Chrome or Edge on Windows is required. The browser allows screen capture only over **HTTPS** or on `localhost`.

## Locally

```bash
python -m http.server 8080
```

Open `http://localhost:8080/`. Opened as `file://` the site does not work: the browser will not load the sound library.

## On a Linux server (nginx)

1. Copy the contents of `web/` to the server, for example to `/var/www/merchant-ear`.
2. Protect the site with a password:
   ```bash
   sudo htpasswd -c /etc/nginx/merchant-ear.htpasswd player
   ```
3. Add the site to nginx (example: [`deploy/nginx.conf`](deploy/nginx.conf)) and get a certificate:
   ```bash
   sudo certbot --nginx -d abi.example.com
   ```
4. On the gaming PC, add the site address to `allowedOrigins` in the client's `appsettings.json`, for example `"https://abi.example.com"`. Otherwise the client refuses the site.

Browsers allow an HTTPS page to connect to `ws://localhost`: `localhost` counts as a secure address. Recent Chrome versions may ask for permission to access devices on the local network on the first connection — allow it.

## Purchases

Your purchases are stored only in this browser (localStorage) and are not sent anywhere. To merge an online purchase base, put its address into `baseRemote` in `config.js` (off by default).
