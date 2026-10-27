// ABI Kaluvan Client: capture of the game sound and the game window.
// The sound is captured with the standard Windows mechanism (process loopback, as in OBS and Discord): the game
// is not opened and nobody touches it, only the process number from the regular process list is needed.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AbiKaluvan.Client
{
    // ---------- WASAPI ----------
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IActivateAudioInterfaceAsyncOperation
    {
        [PreserveSig] int GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
    }

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation);
    }

    // Windows requires an "agile" handler: it may be called from any thread
    [ComImport, Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAgileObject { }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public class ActivationDone : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        public readonly ManualResetEvent Done = new ManualResetEvent(false);
        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation) { Done.Set(); }
    }

    public static class Loopback
    {
        const string VIRTUAL_DEVICE = "VAD\\Process_Loopback";
        const int ACTIVATION_TYPE_PROCESS_LOOPBACK = 1;
        const int MODE_INCLUDE_TARGET_PROCESS_TREE = 0;
        const ushort VT_BLOB = 65;
        const int SHAREMODE_SHARED = 0;
        const uint FLAG_LOOPBACK = 0x00020000, FLAG_EVENTCALLBACK = 0x00040000;
        const uint FLAG_AUTOCONVERTPCM = 0x80000000, FLAG_SRC_DEFAULT_QUALITY = 0x08000000;
        const uint BUFFER_SILENT = 0x2;
        public const int SAMPLE_RATE = 48000;

        [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = true)]
        static extern int ActivateAudioInterfaceAsync([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid riid,
            IntPtr activationParams, IActivateAudioInterfaceCompletionHandler handler, out IActivateAudioInterfaceAsyncOperation operation);

        // exe — name without .exe; "pid:1234" — a specific process (for testing without the game)
        public static int FindProcess(string exe)
        {
            int pid;
            if (exe.StartsWith("pid:") && int.TryParse(exe.Substring(4), out pid)) return Alive(pid) ? pid : 0;
            // polled every 2 s by two threads: the Process objects are released right away
            int found = 0;
            foreach (var p in Process.GetProcessesByName(exe)) { if (found == 0) found = p.Id; p.Dispose(); }
            return found;
        }

        static IAudioClient Activate(int pid)
        {
            // AUDIOCLIENT_ACTIVATION_PARAMS { type, PID, mode } in a PROPVARIANT of type VT_BLOB
            IntPtr prms = Marshal.AllocHGlobal(12), pv = Marshal.AllocHGlobal(24);
            try
            {
                Marshal.WriteInt32(prms, 0, ACTIVATION_TYPE_PROCESS_LOOPBACK);
                Marshal.WriteInt32(prms, 4, pid);
                Marshal.WriteInt32(prms, 8, MODE_INCLUDE_TARGET_PROCESS_TREE);
                for (int i = 0; i < 24; i++) Marshal.WriteByte(pv, i, 0);
                Marshal.WriteInt16(pv, 0, (short)VT_BLOB);
                Marshal.WriteInt32(pv, 8, 12);
                Marshal.WriteIntPtr(pv, IntPtr.Size == 8 ? 16 : 12, prms);

                var iid = typeof(IAudioClient).GUID;
                var done = new ActivationDone();
                IActivateAudioInterfaceAsyncOperation op;
                int hr = ActivateAudioInterfaceAsync(VIRTUAL_DEVICE, ref iid, pv, done, out op);
                if (hr != 0) throw new Exception("Windows refused to capture the sound (code 0x" + hr.ToString("X8") + ")");
                if (!done.Done.WaitOne(5000)) throw new Exception("Windows did not answer the sound capture request");
                int result; object iface;
                Marshal.ThrowExceptionForHR(op.GetActivateResult(out result, out iface));
                if (result != 0) throw new Exception("Windows refused to capture the sound (code 0x" + result.ToString("X8") + ")");
                return (IAudioClient)iface;
            }
            finally { Marshal.FreeHGlobal(prms); Marshal.FreeHGlobal(pv); }
        }

        // Captures the process sound while it is alive and stop() has not returned true. onAudio — mono float 48 kHz.
        public static void Run(int pid, Action<float[]> onAudio, Func<bool> stop)
        {
            var client = Activate(pid);
            IntPtr fmt = Marshal.AllocHGlobal(18);
            try
            {
                // WAVEFORMATEX: float32, 48 kHz, stereo
                Marshal.WriteInt16(fmt, 0, 3);
                Marshal.WriteInt16(fmt, 2, 2);
                Marshal.WriteInt32(fmt, 4, SAMPLE_RATE);
                Marshal.WriteInt32(fmt, 8, SAMPLE_RATE * 8);
                Marshal.WriteInt16(fmt, 12, 8);
                Marshal.WriteInt16(fmt, 14, 32);
                Marshal.WriteInt16(fmt, 16, 0);
                Marshal.ThrowExceptionForHR(client.Initialize(SHAREMODE_SHARED,
                    FLAG_LOOPBACK | FLAG_EVENTCALLBACK | FLAG_AUTOCONVERTPCM | FLAG_SRC_DEFAULT_QUALITY, 2000000, 0, fmt, IntPtr.Zero));
            }
            finally { Marshal.FreeHGlobal(fmt); }

            var ready = new AutoResetEvent(false);
            Marshal.ThrowExceptionForHR(client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle()));
            var ccId = typeof(IAudioCaptureClient).GUID;
            object svc;
            Marshal.ThrowExceptionForHR(client.GetService(ref ccId, out svc));
            var capture = (IAudioCaptureClient)svc;
            Marshal.ThrowExceptionForHR(client.Start());
            var watch = Stopwatch.StartNew();
            try
            {
                float[] stereo = new float[0];
                while (!stop())
                {
                    ready.WaitOne(100);
                    if (watch.ElapsedMilliseconds > 1000)
                    {
                        watch.Restart();
                        if (!Alive(pid)) return;
                    }
                    uint packet;
                    while (capture.GetNextPacketSize(out packet) == 0 && packet > 0)
                    {
                        IntPtr data; uint frames, flags; ulong devPos, qpc;
                        Marshal.ThrowExceptionForHR(capture.GetBuffer(out data, out frames, out flags, out devPos, out qpc));
                        var mono = new float[frames];
                        if ((flags & BUFFER_SILENT) == 0 && frames > 0)
                        {
                            if (stereo.Length < frames * 2) stereo = new float[frames * 2];
                            Marshal.Copy(data, stereo, 0, (int)frames * 2);
                            for (int i = 0; i < frames; i++) mono[i] = (stereo[2 * i] + stereo[2 * i + 1]) * 0.5f;
                        }
                        capture.ReleaseBuffer(frames);
                        if (frames > 0) onAudio(mono);
                    }
                }
            }
            finally
            {
                client.Stop();
                Marshal.ReleaseComObject(capture);
                Marshal.ReleaseComObject(client);
            }
        }

        // Only a check of the process list: the game process itself is not opened (the anti-cheat would not allow it, and it is not needed)
        static bool Alive(int pid)
        {
            try { Process.GetProcessById(pid).Dispose(); return true; } catch { return false; }
        }
    }

    // ---------- screen capture ----------
    // The game window is captured with plain GDI (like a screenshot) and sent as JPEG. Works with the game in
    // "borderless window" mode; in exclusive fullscreen Windows returns a black frame, and the page is told so.
    public static class Screen
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);

        // The system cursor position in screen pixels (the process is DPI aware, so the same pixels as the capture)
        public static bool CursorPos(out int x, out int y)
        {
            Point p;
            bool ok = GetCursorPos(out p);
            x = p.X; y = p.Y;
            return ok;
        }
        delegate bool EnumProc(IntPtr hwnd, IntPtr param);
        [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }

        // The largest visible window of the game process, from the regular Windows window list; the process itself is not opened
        public static IntPtr FindWindow(int pid)
        {
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;
            EnumWindows(delegate (IntPtr h, IntPtr p)
            {
                int owner;
                GetWindowThreadProcessId(h, out owner);
                if (owner != pid || !IsWindowVisible(h)) return true;
                Rect r;
                if (!GetClientRect(h, out r)) return true;
                long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
                if (area > bestArea) { bestArea = area; best = h; }
                return true;
            }, IntPtr.Zero);
            return best;
        }

        // The window client area (without border and title) in screen coordinates
        public static System.Drawing.Rectangle ClientArea(IntPtr hwnd)
        {
            Rect r;
            if (!GetClientRect(hwnd, out r)) return System.Drawing.Rectangle.Empty;
            var p = new Point();
            ClientToScreen(hwnd, ref p);
            return new System.Drawing.Rectangle(p.X, p.Y, r.Right - r.Left, r.Bottom - r.Top);
        }

        static readonly System.Drawing.Imaging.ImageCodecInfo jpeg = FindJpeg();
        static System.Drawing.Imaging.ImageCodecInfo FindJpeg()
        {
            foreach (var c in System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders())
                if (c.MimeType == "image/jpeg") return c;
            return null;
        }

        // Capture of a screen rectangle (the client area of the game window)
        public static System.Drawing.Bitmap Capture(System.Drawing.Rectangle area)
        {
            var bmp = new System.Drawing.Bitmap(area.Width, area.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(area.X, area.Y, 0, 0, area.Size, System.Drawing.CopyPixelOperation.SourceCopy);
            return bmp;
        }

        // Whether the frame is completely black (game in exclusive fullscreen): 81 points in a grid
        public static bool IsBlack(System.Drawing.Bitmap bmp)
        {
            for (int i = 1; i < 10; i++)
                for (int j = 1; j < 10; j++)
                {
                    var px = bmp.GetPixel(bmp.Width * i / 10, bmp.Height * j / 10);
                    if (px.R + px.G + px.B > 30) return false;
                }
            return true;
        }

        // one set of encoder settings for all frames (it holds native memory: a new one per frame was never freed)
        static readonly System.Drawing.Imaging.EncoderParameters jpegQuality = MakeQuality();
        static System.Drawing.Imaging.EncoderParameters MakeQuality()
        {
            var prm = new System.Drawing.Imaging.EncoderParameters(1);
            prm.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
            return prm;
        }

        public static byte[] Encode(System.Drawing.Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                lock (jpegQuality) bmp.Save(ms, jpeg, jpegQuality);
                return ms.ToArray();
            }
        }

    }
}
