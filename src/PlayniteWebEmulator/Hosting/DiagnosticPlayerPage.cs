using PlayniteWebEmulator.Emulation;
using System;
using System.Net;

namespace PlayniteWebEmulator.Hosting
{
    internal static class DiagnosticPlayerPage
    {
        public static string Build(BrowserEmulatorProfile profile, string romPath)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(romPath)) throw new ArgumentException("A ROM path is required.", nameof(romPath));

            return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
                "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                "<title>Web Emulator diagnostic player</title><style>" +
                "html,body{height:100%;margin:0;background:#090d18;color:#e7eefc;font-family:Segoe UI,sans-serif}" +
                "body{display:grid;place-items:center}.card{max-width:760px;margin:24px;padding:32px;border:1px solid #30466f;" +
                "border-radius:18px;background:#111a2d;box-shadow:0 24px 80px #0008}h1{margin-top:0;color:#65d7ff}" +
                "dt{margin-top:16px;color:#91a9cf;font-weight:600}dd{margin:4px 0 0;word-break:break-all}" +
                ".notice{margin-top:28px;padding:14px 16px;border-left:4px solid #f5b942;background:#2c2414}" +
                "</style></head><body><main class=\"card\"><h1>Web Emulator bridge is running</h1>" +
                "<p>This diagnostic page proves the Playnite profile, tracked helper, named-pipe bridge, loopback host, " +
                "and Playnite Chromium window as one lifecycle.</p><dl>" +
                $"<dt>Profile</dt><dd>{Encode(profile.Name)}</dd>" +
                $"<dt>Runtime</dt><dd>{Encode(profile.RuntimeId)}</dd>" +
                $"<dt>Platform</dt><dd>{Encode(profile.PlatformName)}</dd>" +
                $"<dt>Game data</dt><dd>{Encode(romPath)}</dd></dl>" +
                "<div class=\"notice\">The third-party runtime is deliberately not loaded yet. Close this window to end " +
                "the tracked Playnite session.</div></main></body></html>";
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}

