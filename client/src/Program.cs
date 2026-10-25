// ABI Kaluvan Client: entry point.
using System;
using System.Net;
using System.Text;

namespace AbiKaluvan.Client
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "ABI Kaluvan Client";
            var s = Settings.Load(args);
            Console.WriteLine("ABI Kaluvan Client: ws://localhost:" + s.Port + "  (game: " + s.Exe + ".exe)");
            Console.WriteLine("Allowed sites: localhost" + (s.AllowedOrigins.Count > 0 ? ", " + string.Join(", ", s.AllowedOrigins) : ""));
            Console.WriteLine("Capturing sound and picture from the game only. Close this window to stop.");
            try
            {
                Server.Run(s);
            }
            catch (HttpListenerException e)
            {
                Console.WriteLine("Could not open port " + s.Port + ": " + e.Message + " (is the client already running?)");
                Console.WriteLine("Press Enter to exit");
                Console.ReadLine();
                return 1;
            }
            return 0;
        }
    }
}
