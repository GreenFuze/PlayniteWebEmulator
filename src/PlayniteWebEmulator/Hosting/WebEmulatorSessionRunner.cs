using Playnite.SDK;
using PlayniteWebEmulator.Emulation;
using PlayniteWebEmulator.Runtime;
using System;
using System.IO;
using System.Windows.Media;

namespace PlayniteWebEmulator.Hosting
{
    internal sealed class WebEmulatorSessionRunner
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly IPlayniteAPI playniteApi;
        private readonly EmulatorJsRuntimeInstaller emulatorJsRuntimeInstaller;

        public WebEmulatorSessionRunner(
            IPlayniteAPI playniteApi,
            EmulatorJsRuntimeInstaller emulatorJsRuntimeInstaller)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            this.emulatorJsRuntimeInstaller = emulatorJsRuntimeInstaller
                ?? throw new ArgumentNullException(nameof(emulatorJsRuntimeInstaller));
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

            if (!string.Equals(profile.RuntimeId, "emulatorjs", StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"The {profile.RuntimeId} web runtime is not implemented yet. Choose an EmulatorJS profile for this game.");
            }

            var runtimeDataPath = EnsureEmulatorJsRuntime();
            var gameName = Path.GetFileNameWithoutExtension(fullRomPath);
            var html = EmulatorJsPlayerPage.Build(profile, gameName);
            using (var webView = playniteApi.WebViews.CreateView(new WebViewSettings
            {
                JavaScriptEnabled = true,
                WindowWidth = 1280,
                WindowHeight = 800,
                WindowBackground = Colors.Black
            }))
            using (var fullScreenController = new PlayerWindowFullScreenController(webView.WindowHost))
            using (var server = new EmulatorLoopbackWebServer(
                html,
                runtimeDataPath,
                fullRomPath,
                diagnostic =>
                {
                    Logger.Info($"EmulatorJS [{profile.Id}] {diagnostic}");
                    fullScreenController.Handle(diagnostic);
                }))
            {
                Logger.Info($"Opening EmulatorJS player for '{gameName}' with profile '{profile.Id}' at {server.Address}.");
                webView.Navigate(server.Address.AbsoluteUri);
                webView.WindowHost.Title = $"{profile.Name} — Web Emulator";
                webView.OpenDialog();
                Logger.Info($"EmulatorJS player for '{gameName}' closed.");
            }
        }

        private string EnsureEmulatorJsRuntime()
        {
            string runtimeDataPath = null;
            var result = playniteApi.Dialogs.ActivateGlobalProgress(
                async progressArgs =>
                {
                    runtimeDataPath = await emulatorJsRuntimeInstaller.EnsureInstalledAsync(
                        update => UpdateProgress(progressArgs, update),
                        progressArgs.CancelToken).ConfigureAwait(false);
                },
                new GlobalProgressOptions("Preparing EmulatorJS", true)
                {
                    IsIndeterminate = false
                });

            if (result.Canceled)
            {
                throw new OperationCanceledException("EmulatorJS setup was canceled.");
            }

            if (result.Error != null)
            {
                throw new InvalidOperationException("EmulatorJS setup failed.", result.Error);
            }

            if (string.IsNullOrWhiteSpace(runtimeDataPath))
            {
                throw new InvalidOperationException("EmulatorJS setup did not produce a usable runtime.");
            }

            return runtimeDataPath;
        }

        private static void UpdateProgress(GlobalProgressActionArgs progressArgs, RuntimeInstallProgress update)
        {
            if (progressArgs == null) throw new ArgumentNullException(nameof(progressArgs));
            if (update == null) throw new ArgumentNullException(nameof(update));
            progressArgs.ProgressMaxValue = Math.Max(1, update.TotalBytes);
            progressArgs.CurrentProgressValue = Math.Min(update.CompletedBytes, (long)progressArgs.ProgressMaxValue);
            progressArgs.Text = update.Phase;
            progressArgs.IsIndeterminate = false;
        }
    }
}
