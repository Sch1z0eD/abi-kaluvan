// The site (web/) built into the exe as resources "web/<path>": served as is, read once and kept in memory.
// config.js is generated: the page talks to this same client, whatever port it runs on.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace MerchantEar.Client
{
    public static class SiteFiles
    {
        static readonly Assembly asm = typeof(SiteFiles).Assembly;
        static readonly Dictionary<string, string> names = Index();
        static readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();

        static readonly Dictionary<string, string> types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { ".html", "text/html; charset=utf-8" }, { ".js", "application/javascript; charset=utf-8" },
            { ".css", "text/css; charset=utf-8" }, { ".json", "application/json; charset=utf-8" }, { ".svg", "image/svg+xml" },
            { ".png", "image/png" }, { ".ico", "image/x-icon" }, { ".wav", "audio/wav" }, { ".gz", "application/gzip" },
            { ".wasm", "application/wasm" },
        };

        // resource names use "\" for folders when built on Windows: looked up by a normalized path
        static Dictionary<string, string> Index()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in asm.GetManifestResourceNames())
                if (n.StartsWith("web/", StringComparison.Ordinal) || n.StartsWith("web\\", StringComparison.Ordinal))
                    map[n.Substring(4).Replace('\\', '/')] = n;
            return map;
        }

        public static bool Has { get { return names.Count > 0; } }

        public static bool Serve(HttpListenerContext ctx, string path)
        {
            string rel = Uri.UnescapeDataString(path).TrimStart('/');
            if (rel.Length == 0) rel = "index.html";
            byte[] body;
            if (rel == "config.js")
            {
                // the page is served by this client: it connects back to the same host and port
                body = Encoding.UTF8.GetBytes("window.ABI_CONFIG = { clientUrl: 'ws://' + location.host, baseRemote: '' };\n");
            }
            else
            {
                string name;
                if (!names.TryGetValue(rel, out name)) return false;
                lock (cache)
                {
                    if (!cache.TryGetValue(name, out body))
                    {
                        using (var s = asm.GetManifestResourceStream(name))
                        using (var ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            body = ms.ToArray();
                        }
                        cache[name] = body;
                    }
                }
            }
            var res = ctx.Response;
            string type;
            res.ContentType = types.TryGetValue(Path.GetExtension(rel), out type) ? type : "application/octet-stream";
            res.Headers["Cache-Control"] = "no-cache";
            res.ContentLength64 = body.Length;
            res.OutputStream.Write(body, 0, body.Length);
            res.Close();
            return true;
        }
    }
}
