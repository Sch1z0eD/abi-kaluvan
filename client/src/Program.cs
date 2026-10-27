// Merchant Ear: entry point. One exe: the site and the client together on http://localhost:PORT/.
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace MerchantEar.Client
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Merchant Ear";
            var s = Settings.Load(args);
            string site = "http://localhost:" + s.Port + "/";
            Console.WriteLine("Merchant Ear: " + (SiteFiles.Has ? site : "ws://localhost:" + s.Port) + "  (game: " + s.Exe + ".exe)");
            Console.WriteLine("Allowed sites: localhost" + (s.AllowedOrigins.Count > 0 ? ", " + string.Join(", ", s.AllowedOrigins) : ""));
            Console.WriteLine("Capturing sound and picture from the game only. Close this window to stop.");
            try
            {
                Server.Run(s, delegate { if (s.OpenBrowser && SiteFiles.Has) Open(site); });
            }
            catch (HttpListenerException e)
            {
                // a second launch: the first one is still running, just show its page
                if (Running(site))
                {
                    Console.WriteLine("Merchant Ear is already running: opening " + site);
                    if (s.OpenBrowser && SiteFiles.Has) Open(site);
                    return 0;
                }
                Console.WriteLine("Could not open port " + s.Port + ": " + e.Message + " (busy? try --port 8778)");
                Console.WriteLine("Press Enter to exit");
                Console.ReadLine();
                return 1;
            }
            return 0;
        }

        static bool Running(string site)
        {
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
                    return http.GetStringAsync(site + "status").Result.Contains("\"type\":\"status\"");
            }
            catch { return false; }
        }

        static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception e) { Console.WriteLine("Open " + url + " in the browser (" + e.Message + ")"); }
        }
    }
}
