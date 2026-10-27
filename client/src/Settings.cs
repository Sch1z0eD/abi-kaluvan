// Client settings: appsettings.json next to the exe, then command-line overrides.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MerchantEar.Client
{
    public sealed class Settings
    {
        public int Port { get; set; } = 8777;
        // Game process name without .exe; "pid:1234" — a specific process (for testing without the game)
        public string Exe { get; set; } = "UAGame";
        // Sites allowed to connect, e.g. "https://abi.example.com". Pages on localhost / 127.0.0.1 are always allowed.
        public List<string> AllowedOrigins { get; set; } = new List<string>();
        // Open the site in the default browser on start
        public bool OpenBrowser { get; set; } = true;

        public static Settings Load(string[] args)
        {
            var s = new Settings();
            string file = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(file))
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
                s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file), opts) ?? s;
            }
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "--port": s.Port = int.Parse(next); i++; break;
                    case "--exe": s.Exe = next; i++; break;
                    case "--origin": s.AllowedOrigins.Add(next); i++; break;
                    case "--no-browser": s.OpenBrowser = false; break;
                }
            }
            for (int i = 0; i < s.AllowedOrigins.Count; i++) s.AllowedOrigins[i] = s.AllowedOrigins[i].TrimEnd('/');
            return s;
        }

        // Browsers always send Origin with a WebSocket handshake; without it this is not a web page (curl, a test tool)
        public bool OriginAllowed(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return true;
            Uri u;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out u)) return false;
            if ((u.Scheme == "http" || u.Scheme == "https") && (u.Host == "localhost" || u.Host == "127.0.0.1" || u.Host == "[::1]")) return true;
            foreach (var o in AllowedOrigins) if (string.Equals(o, origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
