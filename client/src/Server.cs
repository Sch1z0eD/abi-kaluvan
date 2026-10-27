// Merchant Ear Client server: streams the game sound and the game window to the site over WebSocket.
// It serves no files: the site lives separately (web/), on any server or on localhost.
// Only pages from the allowed origins (settings: allowedOrigins) may connect, so a random site
// opened in the same browser cannot get the game picture or sound.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace MerchantEar.Client
{
    public static class Server
    {
        static readonly List<Peer> clients = new List<Peer>();        // pages listening to the game sound
        static readonly List<Peer> screenClients = new List<Peer>();  // pages taking the screen frames
        const int SCREEN_FRAME_MS = 33;  // up to 30 frames per second
        const int BLACK_CHECK_EVERY = 15; // check for a black frame once per this many frames
        static string status = "{\"type\":\"status\",\"state\":\"start\"}";
        static Settings settings;

        // Blocks the thread: serves requests until the window is closed
        public static void Run(Settings s)
        {
            settings = s;
            Screen.SetProcessDPIAware(); // otherwise only part of the screen is captured at 125% Windows scaling
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:" + s.Port + "/");
            listener.Start();
            var t = new Thread(delegate () { CaptureLoop(s.Exe); });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
            var sc = new Thread(delegate () { ScreenLoop(s.Exe); });
            sc.IsBackground = true;
            sc.Start();
            while (listener.IsListening)
            {
                var ctx = listener.GetContext();
                ThreadPool.QueueUserWorkItem(delegate { Handle(ctx); });
            }
        }

        static void Handle(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath;
                string origin = ctx.Request.Headers["Origin"];
                if (!settings.OriginAllowed(origin))
                {
                    Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  refused a connection from " + origin + " (not in allowedOrigins)");
                    Reply(ctx, 403, "{\"error\":\"origin not allowed\"}", null);
                    return;
                }
                if (path == "/audio" && ctx.Request.IsWebSocketRequest) { Audio(ctx); return; }
                if (path == "/screen" && ctx.Request.IsWebSocketRequest) { ScreenSocket(ctx); return; }
                if (path == "/status") { Reply(ctx, 200, status, origin); return; }
                Reply(ctx, 404, "{\"error\":\"not found\"}", origin);
            }
            catch { try { ctx.Response.Abort(); } catch { } }
        }

        static void Reply(HttpListenerContext ctx, int code, string json, string origin)
        {
            var res = ctx.Response;
            res.StatusCode = code;
            res.ContentType = "application/json; charset=utf-8";
            if (origin != null) { res.Headers["Access-Control-Allow-Origin"] = origin; res.Headers["Vary"] = "Origin"; }
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.Close();
        }

        static void Audio(HttpListenerContext ctx)
        {
            var peer = new Peer(ctx.AcceptWebSocketAsync(null).Result.WebSocket);
            lock (clients) clients.Add(peer);
            peer.Post(Encoding.UTF8.GetBytes(status), WebSocketMessageType.Text);
            WaitClosed(peer.Ws);
            lock (clients) clients.Remove(peer);
            peer.Close();
        }

        static void ScreenSocket(HttpListenerContext ctx)
        {
            var peer = new Peer(ctx.AcceptWebSocketAsync(null).Result.WebSocket);
            lock (screenClients) screenClients.Add(peer);
            peer.Post(Encoding.UTF8.GetBytes(ScreenStateJson()), WebSocketMessageType.Text);
            // a new page gets the cursor position at once, not only after the next mouse move
            if (lastCursor != null) peer.Post(Encoding.UTF8.GetBytes(lastCursor), WebSocketMessageType.Text);
            WaitClosed(peer.Ws);
            lock (screenClients) screenClients.Remove(peer);
            peer.Close();
        }

        // the page sends nothing; we read only to notice that it was closed
        static void WaitClosed(WebSocket ws)
        {
            var buf = new ArraySegment<byte>(new byte[256]);
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var r = ws.ReceiveAsync(buf, CancellationToken.None).Result;
                    if (r.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch { }
        }

        // To every page in a list: each one has its own queue (Peer), nobody waits for a slow page
        static void Post(List<Peer> list, byte[] data, WebSocketMessageType type, bool latestOnly = false)
        {
            Peer[] peers;
            lock (list) peers = list.ToArray();
            foreach (var p in peers) p.Post(data, type, latestOnly);
        }

        // Screen frames — only while the page waits for them. Capture and JPEG encoding run in parallel
        // in two threads (for the right half of the window — ~8 and ~3 ms): up to 30 frames per second.
        static System.Drawing.Bitmap pending;    // a captured frame that is not encoded yet
        static string screenState = "start";     // game | nogame | minimized

        static void SetScreenState(string state)
        {
            if (state == screenState) return;
            screenState = state;
            Post(screenClients, Encoding.UTF8.GetBytes(ScreenStateJson()), WebSocketMessageType.Text);
        }

        // crop: the frame is already the right part of the window (the merchant is always on the right), the page does not crop again
        const double MERCHANT_SIDE = 0.5;
        static string ScreenStateJson() { return "{\"type\":\"screen\",\"state\":\"" + screenState + "\",\"crop\":" + MERCHANT_SIDE.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"; }
        static readonly AutoResetEvent hasPending = new AutoResetEvent(false);

        // Capture only the client area of the game window: other monitors and apps never get into the frame
        static void ScreenLoop(string exe)
        {
            var enc = new Thread(EncodeLoop);
            enc.IsBackground = true;
            enc.Start();
            var watch = Stopwatch.StartNew();
            IntPtr hwnd = IntPtr.Zero;
            long lookedAt = -100000;
            while (true)
            {
                int count;
                lock (screenClients) count = screenClients.Count;
                if (count == 0) { Thread.Sleep(200); continue; }
                long started = watch.ElapsedMilliseconds;
                if (hwnd == IntPtr.Zero || started - lookedAt > 2000)
                {
                    lookedAt = started;
                    int pid = Loopback.FindProcess(exe);
                    hwnd = pid != 0 ? Screen.FindWindow(pid) : IntPtr.Zero;
                }
                var area = hwnd == IntPtr.Zero || Screen.IsIconic(hwnd) ? System.Drawing.Rectangle.Empty : Screen.ClientArea(hwnd);
                SetScreenState(hwnd == IntPtr.Zero ? "nogame" : area.Width < 100 || area.Height < 100 ? "minimized" : "game");
                if (screenState != "game") { Thread.Sleep(500); continue; }
                try
                {
                    // only the right part of the window: the merchant is there; capturing and encoding the left half is wasted
                    int cut = (int)Math.Round(area.Width * MERCHANT_SIDE);
                    var bmp = Screen.Capture(new System.Drawing.Rectangle(area.X + cut, area.Y, area.Width - cut, area.Height));
                    SendCursor(area.X + cut, area.Y, area.Width - cut, area.Height);
                    // if the previous one is not encoded yet, replace it with the fresh one, so the queue does not grow
                    var old = Interlocked.Exchange(ref pending, bmp);
                    if (old != null) old.Dispose();
                    hasPending.Set();
                }
                catch (Exception e) { Console.WriteLine("screen capture failed: " + e.Message); Thread.Sleep(1000); }
                int rest = SCREEN_FRAME_MS - (int)(watch.ElapsedMilliseconds - started);
                if (rest > 0) Thread.Sleep(rest);
            }
        }

        // Where the mouse cursor is, in frame pixels (the frame is the captured right part of the window), sent with
        // every frame. The page takes the cargo under the cursor as the hovered one and, at the moment a pick-up
        // sound starts, as the dragged one. Only the system cursor position is read (GetCursorPos, as any app can);
        // the game is not touched. The frame itself never contains the cursor.
        static string lastCursor = null;
        static void SendCursor(int x0, int y0, int w, int h)
        {
            int x, y;
            if (!Screen.CursorPos(out x, out y)) return;
            x -= x0; y -= y0;
            bool inside = x >= 0 && y >= 0 && x < w && y < h;
            string json = "{\"type\":\"cursor\",\"x\":" + x + ",\"y\":" + y + ",\"in\":" + (inside ? "true" : "false") + "}";
            if (json == lastCursor) return; // the page keeps the last position: an unchanged one is not resent
            lastCursor = json;
            Post(screenClients, Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text);
        }

        static void EncodeLoop()
        {
            bool wasBlack = false;
            int n = 0;
            while (true)
            {
                hasPending.WaitOne();
                var bmp = Interlocked.Exchange(ref pending, null);
                if (bmp == null) continue;
                try
                {
                    if (n++ % BLACK_CHECK_EVERY == 0)
                    {
                        bool black = Screen.IsBlack(bmp);
                        if (black != wasBlack)
                        {
                            wasBlack = black;
                            var msg = Encoding.UTF8.GetBytes("{\"type\":\"screen\",\"black\":" + (black ? "true" : "false") + "}");
                            Post(screenClients, msg, WebSocketMessageType.Text);
                        }
                    }
                    byte[] frame = Screen.Encode(bmp);
                    Post(screenClients, frame, WebSocketMessageType.Binary, true); // only the latest frame waits
                }
                catch (Exception e) { Console.WriteLine("screen frame failed: " + e.Message); }
                finally { bmp.Dispose(); }
            }
        }

        static void Broadcast(byte[] data, WebSocketMessageType type)
        {
            Post(clients, data, type);
        }

        static void SetStatus(string state, int pid, string message)
        {
            status = "{\"type\":\"status\",\"state\":\"" + state + "\",\"pid\":" + pid + ",\"rate\":" + Loopback.SAMPLE_RATE +
                     ",\"message\":\"" + (message ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            Broadcast(Encoding.UTF8.GetBytes(status), WebSocketMessageType.Text);
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + (state == "game" ? "listening to the game (PID " + pid + ")" : state == "wait" ? "game is not running, waiting for " + message : "error: " + message));
        }

        static void CaptureLoop(string exe)
        {
            string last = null;
            while (true)
            {
                int pid = Loopback.FindProcess(exe);
                if (pid == 0)
                {
                    if (last != "wait") SetStatus("wait", 0, exe + ".exe");
                    last = "wait";
                    Thread.Sleep(2000);
                    continue;
                }
                try
                {
                    SetStatus("game", pid, exe + ".exe");
                    last = "game";
                    Loopback.Run(pid, delegate (float[] mono)
                    {
                        var bytes = new byte[mono.Length * 4];
                        Buffer.BlockCopy(mono, 0, bytes, 0, bytes.Length);
                        Broadcast(bytes, WebSocketMessageType.Binary);
                    }, delegate { return false; });
                }
                catch (Exception e)
                {
                    SetStatus("error", pid, e.Message);
                    last = "error";
                    Thread.Sleep(3000);
                }
            }
        }
    }
}
