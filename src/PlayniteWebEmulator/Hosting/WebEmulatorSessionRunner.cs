using Playnite.SDK;
using PlayniteWebEmulator.Emulation;
using System;
using System.IO;
using System.Windows.Media;

namespace PlayniteWebEmulator.Hosting
{
    internal sealed class WebEmulatorSessionRunner
    {
        private readonly IPlayniteAPI playniteApi;

        public WebEmulatorSessionRunner(IPlayniteAPI playniteApi)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
        }

        public void Run(BrowserEmulatorProfile profile, string romPath)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(romPath)) throw new ArgumentException("A ROM path is required.", nameof(romPath));

            var fullRomPath = Path.GetFullPath(romPath);
            if (!File.Exists(fullRomPath))
            {
                throw new FileNotFoundException("The selected game file does not exist.", fullRomPath);
            }

            var html = DiagnosticPlayerPage.Build(profile, fullRomPath);
            using (var server = new LoopbackWebServer(html))
            using (var webView = playniteApi.WebViews.CreateView(new WebViewSettings
            {
                JavaScriptEnabled = true,
                WindowWidth = 1280,
                WindowHeight = 800,
                WindowBackground = Colors.Black
            }))
            {
                webView.Navigate(server.Address.AbsoluteUri);
                webView.WindowHost.Title = $"{profile.Name} — Web Emulator";
                webView.OpenDialog();
            }
        }
    }
}

