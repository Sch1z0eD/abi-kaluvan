# Merchant Ear Client (`client/`)

Консольное приложение для Windows на .NET 10. Захватывает звук только процесса игры, кадры только её окна и положение курсора и отдаёт их сайту по WebSocket. У каждой страницы своя очередь: медленная вкладка не тормозит захват и других. Файлы сайта не раздаёт.

| Файл | Что делает |
|---|---|
| `src/Capture.cs` | Захват звука процесса через WASAPI process loopback (как OBS и Discord) и снимки окна игры через GDI. Память игры не читается, процесс не открывается |
| `src/Server.cs` | `ws://localhost:8777/audio` (48 кГц моно float), `ws://localhost:8777/screen` (JPEG правой половины окна, до 30 кадров/с), `GET /status` |
| `src/Settings.cs` | Настройки и проверка, каким сайтам можно подключаться |
| `appsettings.json` | Порт, имя процесса игры, разрешённые сайты |

## Требования

- Windows 11 или Windows 10 сборки 20348 и новее (нужен захват звука отдельного процесса).
- .NET 10 SDK для сборки; для запуска готового `.exe` — .NET Desktop Runtime 10.
- Игра в режиме «Окно без рамки» (Borderless). В полноэкранном режиме кадр будет чёрным.

## Сборка и запуск

```bash
dotnet run -c Release
```

Готовый `.exe` в папку `publish/`:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

## Настройки

`appsettings.json` рядом с `.exe`:

```json
{
  "port": 8777,
  "exe": "UAGame",
  "allowedOrigins": ["https://abi.example.com"]
}
```

- Страницы с `localhost` и `127.0.0.1` разрешены всегда. Любой другой сайт получит отказ 403, и это будет видно в окне клиента.
- Те же параметры можно задать в командной строке: `--port 8799 --exe UAGame --origin https://abi.example.com`.
- Если меняете порт, поменяйте и `clientUrl` в `web/config.js`.

Проверить, что клиент работает:

```bash
curl http://localhost:8777/status
```
