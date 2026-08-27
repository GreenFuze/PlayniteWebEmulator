using Playnite.SDK;
using PlayniteWebEmulator.Emulation;
using PlayniteWebEmulator.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace PlayniteWebEmulator.Hosting
{
    internal sealed class WebEmulatorSessionRunner : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly IPlayniteAPI playniteApi;
        private readonly EmulatorJsRuntimeInstaller emulatorJsRuntimeInstaller;
        private readonly JsDosRuntimeInstaller jsDosRuntimeInstaller;
        private readonly ScummVmRuntimeInstaller scummVmRuntimeInstaller;
        private readonly JsDosLaunchResolver jsDosLaunchResolver;
        private readonly ScummVmEngineResolver scummVmEngineResolver;
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();

        public WebEmulatorSessionRunner(
            IPlayniteAPI playniteApi,
            EmulatorJsRuntimeInstaller emulatorJsRuntimeInstaller,
            JsDosRuntimeInstaller jsDosRuntimeInstaller,
            ScummVmRuntimeInstaller scummVmRuntimeInstaller,
            JsDosLaunchResolver jsDosLaunchResolver,
            ScummVmEngineResolver scummVmEngineResolver)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            this.emulatorJsRuntimeInstaller = emulatorJsRuntimeInstaller
                ?? throw new ArgumentNullException(nameof(emulatorJsRuntimeInstaller));
            this.jsDosRuntimeInstaller = jsDosRuntimeInstaller
                ?? throw new ArgumentNullException(nameof(jsDosRuntimeInstaller));
            this.scummVmRuntimeInstaller = scummVmRuntimeInstaller
                ?? throw new ArgumentNullException(nameof(scummVmRuntimeInstaller));
            this.jsDosLaunchResolver = jsDosLaunchResolver
                ?? throw new ArgumentNullException(nameof(jsDosLaunchResolver));
            this.scummVmEngineResolver = scummVmEngineResolver
                ?? throw new ArgumentNullException(nameof(scummVmEngineResolver));
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

            if (string.Equals(profile.RuntimeId, "scummvm", StringComparison.Ordinal))
            {
                RunScummVm(profile, fullRomPath);
                return;
            }

            if (string.Equals(profile.RuntimeId, "jsdos", StringComparison.Ordinal))
            {
                RunJsDos(profile, fullRomPath);
                return;
            }

            if (!string.Equals(profile.RuntimeId, "emulatorjs", StringComparison.Ordinal))
                throw new NotSupportedException($"The {profile.RuntimeId} web runtime is not implemented yet.");

            RunEmulatorJs(profile, fullRomPath);
        }

        private void RunJsDos(BrowserEmulatorProfile profile, string markerPath)
        {
            var plan = jsDosLaunchResolver.Resolve(markerPath, SelectJsDosLauncher);
            var runtimeRoot = EnsureJsDosRuntime();
            var gameName = Path.GetFileName(plan.GameRoot);
            var html = JsDosPlayerPage.Build(gameName, plan);
            using (var server = EmulatorLoopbackWebServer.ForGameDirectory(
                html,
                runtimeRoot,
                plan.GameRoot,
                plan.Files.Select(file => file.RelativePath),
                diagnostic =>
                {
                    if (!string.Equals(diagnostic.EventName, "heartbeat", StringComparison.OrdinalIgnoreCase))
                        Logger.Info($"js-dos [{profile.Id}] {diagnostic}");
                }))
            {
                Logger.Info($"Opening js-dos player for '{gameName}' with launcher '{plan.LaunchRelativePath}' at {server.Address}.");
                Process.Start(new ProcessStartInfo(server.Address.AbsoluteUri) { UseShellExecute = true });
                server.WaitForSessionEnd(shutdown.Token);
                Logger.Info($"js-dos browser player for '{gameName}' closed.");
            }
        }

        private void RunEmulatorJs(BrowserEmulatorProfile profile, string fullRomPath)
        {

            var runtimeDataPath = EnsureEmulatorJsRuntime();
            var gameFileName = Path.GetFileName(fullRomPath);
            var gameName = Path.GetFileNameWithoutExtension(fullRomPath);
            var html = EmulatorJsPlayerPage.Build(profile, gameName, gameFileName);
            using (var server = new EmulatorLoopbackWebServer(
                html,
                runtimeDataPath,
                fullRomPath,
                diagnostic =>
                {
                    if (!string.Equals(diagnostic.EventName, "heartbeat", StringComparison.OrdinalIgnoreCase))
                        Logger.Info($"EmulatorJS [{profile.Id}] {diagnostic}");
                }))
            {
                Logger.Info($"Opening EmulatorJS player for '{gameName}' with profile '{profile.Id}' in the default browser at {server.Address}.");
                Process.Start(new ProcessStartInfo(server.Address.AbsoluteUri) { UseShellExecute = true });
                server.WaitForSessionEnd(shutdown.Token);
                Logger.Info($"Browser player for '{gameName}' closed.");
            }
        }

        private void RunScummVm(BrowserEmulatorProfile profile, string markerPath)
        {
            var plan = scummVmEngineResolver.Resolve(markerPath);
            var runtimeRoot = EnsureScummVmRuntime(plan.EnginePluginFileName);
            var gameName = Path.GetFileName(plan.GameRoot);
            var html = ScummVmPlayerPage.Build(gameName, plan);
            using (var server = EmulatorLoopbackWebServer.ForGameDirectory(
                html,
                runtimeRoot,
                plan.GameRoot,
                plan.Files.Select(file => file.RelativePath),
                diagnostic =>
                {
                    if (!string.Equals(diagnostic.EventName, "heartbeat", StringComparison.OrdinalIgnoreCase))
                        Logger.Info($"ScummVM [{profile.Id}] {diagnostic}");
                }))
            {
                Logger.Info($"Opening ScummVM player for '{gameName}' with engine '{plan.EnginePluginFileName}' at {server.Address}.");
                Process.Start(new ProcessStartInfo(server.Address.AbsoluteUri) { UseShellExecute = true });
                server.WaitForSessionEnd(shutdown.Token);
                Logger.Info($"ScummVM browser player for '{gameName}' closed.");
            }
        }

        public void Stop() => shutdown.Cancel();

        public void Dispose() => shutdown.Dispose();

        private string EnsureEmulatorJsRuntime()
        {
            string runtimeDataPath = null;
            GlobalProgressResult result = null;
            playniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                result = playniteApi.Dialogs.ActivateGlobalProgress(
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

        private string EnsureScummVmRuntime(string enginePluginFileName)
        {
            string runtimeRoot = null;
            GlobalProgressResult result = null;
            playniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                result = playniteApi.Dialogs.ActivateGlobalProgress(
                    async progressArgs =>
                    {
                        runtimeRoot = await scummVmRuntimeInstaller.EnsureInstalledAsync(
                            enginePluginFileName,
                            update => UpdateProgress(progressArgs, update),
                            progressArgs.CancelToken).ConfigureAwait(false);
                    },
                    new GlobalProgressOptions("Preparing ScummVM", true)
                    {
                        IsIndeterminate = false
                    });
            });

            if (result.Canceled) throw new OperationCanceledException("ScummVM setup was canceled.");
            if (result.Error != null) throw new InvalidOperationException("ScummVM setup failed.", result.Error);
            if (string.IsNullOrWhiteSpace(runtimeRoot)) throw new InvalidOperationException("ScummVM setup did not produce a usable runtime.");
            return runtimeRoot;
        }

        private string EnsureJsDosRuntime()
        {
            string runtimeRoot = null;
            GlobalProgressResult result = null;
            playniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                result = playniteApi.Dialogs.ActivateGlobalProgress(
                    async progressArgs =>
                    {
                        runtimeRoot = await jsDosRuntimeInstaller.EnsureInstalledAsync(
                            update => UpdateProgress(progressArgs, update),
                            progressArgs.CancelToken).ConfigureAwait(false);
                    },
                    new GlobalProgressOptions("Preparing js-dos", true)
                    {
                        IsIndeterminate = false
                    });
            });

            if (result.Canceled) throw new OperationCanceledException("js-dos setup was canceled.");
            if (result.Error != null) throw new InvalidOperationException("js-dos setup failed.", result.Error);
            if (string.IsNullOrWhiteSpace(runtimeRoot)) throw new InvalidOperationException("js-dos setup did not produce a usable runtime.");
            return runtimeRoot;
        }

        private string SelectJsDosLauncher(JsDosLaunchSelectionRequest request)
        {
            string selectedPath = null;
            playniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                var options = request.Candidates.Select(candidate => new GenericItemOption
                {
                    Name = Path.GetFileName(candidate.RelativePath),
                    Description = candidate.RelativePath
                }).ToList();
                List<GenericItemOption> Search(string query)
                {
                    if (string.IsNullOrWhiteSpace(query)) return options;
                    return options.Where(option =>
                        option.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        option.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }
                selectedPath = playniteApi.Dialogs.ChooseItemWithSearch(
                    options,
                    Search,
                    caption: $"Choose DOS launcher for {request.GameName}")?.Description;
            });
            return selectedPath;
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
