// One connected page: its own sending thread and queue, so a slow page (a hidden tab, a busy browser) never holds up
// the sound capture, the screen capture or the other pages.
//  * Screen frames: only the latest one waits; an older frame not sent yet is replaced (the page wants fresh frames).
//  * Everything else (sound packets, cursor, status): in order, at most MAX_QUEUE; on overflow the oldest is dropped.
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;

namespace MerchantEar.Client
{
    public sealed class Peer
    {
        const int MAX_QUEUE = 400;          // ~4 s of game sound in 10 ms packets
        const int SEND_TIMEOUT_MS = 10000;  // a page that takes no data for this long is gone: dropped

        public readonly WebSocket Ws;
        readonly object gate = new object();
        readonly Queue<KeyValuePair<byte[], WebSocketMessageType>> queue = new Queue<KeyValuePair<byte[], WebSocketMessageType>>();
        byte[] frame;                       // the latest screen frame not sent yet
        readonly AutoResetEvent ready = new AutoResetEvent(false);
        volatile bool closed;

        public Peer(WebSocket ws)
        {
            Ws = ws;
            var t = new Thread(Loop);
            t.IsBackground = true;
            t.Start();
        }

        public void Post(byte[] data, WebSocketMessageType type, bool latestOnly = false)
        {
            if (closed) return;
            lock (gate)
            {
                if (latestOnly) frame = data;
                else
                {
                    if (queue.Count >= MAX_QUEUE) queue.Dequeue();
                    queue.Enqueue(new KeyValuePair<byte[], WebSocketMessageType>(data, type));
                }
            }
            ready.Set();
        }

        void Loop()
        {
            try
            {
                while (!closed && Ws.State == WebSocketState.Open)
                {
                    ready.WaitOne(500);
                    while (true)
                    {
                        byte[] data;
                        WebSocketMessageType type;
                        lock (gate)
                        {
                            if (queue.Count > 0) { var m = queue.Dequeue(); data = m.Key; type = m.Value; }
                            else if (frame != null) { data = frame; frame = null; type = WebSocketMessageType.Binary; }
                            else break;
                        }
                        using (var cts = new CancellationTokenSource(SEND_TIMEOUT_MS))
                            Ws.SendAsync(new ArraySegment<byte>(data), type, true, cts.Token).GetAwaiter().GetResult();
                    }
                }
            }
            catch { }
            Close();
        }

        public void Close()
        {
            closed = true;
            try { Ws.Abort(); } catch { }
        }
    }
}
