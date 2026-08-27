using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using PlayniteWebEmulator.Emulation;
using PlayniteWebEmulator.Hosting;
using PlayniteWebEmulator.Interop;
using PlayniteWebEmulator.Protocol;
using PlayniteWebEmulator.Runtime;
using System;
using System.IO;

namespace PlayniteWebEmulator
{
    public sealed class WebEmulatorPlugin : GenericPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly BrowserEmulatorProfileCatalog catalog;
        private readonly PlayniteEmulatorRegistrar registrar;
        private readonly EmulatorJsRuntimeInstaller emulatorJsRuntimeInstaller;
        private readonly JsDosRuntimeInstaller jsDosRuntimeInstaller;
        private readonly ScummVmRuntimeInstaller scummVmRuntimeInstaller;
        private readonly WebEmulatorSessionRunner sessionRunner;
        private readonly LaunchPipeServer pipeServer;

        public static readonly Guid PluginId = Guid.Parse("41d5bc40-a7e8-46a6-888e-d52cf719c397");
        public override Guid Id => PluginId;

        public WebEmulatorPlugin(IPlayniteAPI playniteApi)
            : base(playniteApi ?? throw new ArgumentNullException(nameof(playniteApi)))
        {
            var pluginDirectory = Path.GetDirectoryName(typeof(WebEmulatorPlugin).Assembly.Location)
                ?? throw new InvalidOperationException("The Web Emulator plugin directory could not be resolved.");
            catalog = new BrowserEmulatorProfileCatalog();
            registrar = new PlayniteEmulatorRegistrar(PlayniteApi.Database, PlayniteApi.Emulation, catalog, pluginDirectory);
            emulatorJsRuntimeInstaller = new EmulatorJsRuntimeInstaller(GetPluginUserDataPath());
            jsDosRuntimeInstaller = new JsDosRuntimeInstaller(GetPluginUserDataPath());
            scummVmRuntimeInstaller = new ScummVmRuntimeInstaller(GetPluginUserDataPath());
            sessionRunner = new WebEmulatorSessionRunner(
                PlayniteApi,
                emulatorJsRuntimeInstaller,
                jsDosRuntimeInstaller,
                scummVmRuntimeInstaller,
                new JsDosLaunchResolver(),
                new ScummVmEngineResolver());
            pipeServer = new LaunchPipeServer(HandleLaunch);
            Properties = new GenericPluginProperties { HasSettings = false };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            try
            {
                var emulator = registrar.Synchronize();
                pipeServer.Start();
                Logger.Info($"Web Emulator registered {emulator.CustomProfiles.Count} managed profile(s).");
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Web Emulator failed to initialize.");
                PlayniteApi.Notifications.Add(
                    "playnite-web-emulator-initialization",
                    $"Web Emulator could not initialize: {exception.GetBaseException().Message}",
                    NotificationType.Error);
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            sessionRunner.Stop();
            pipeServer.Stop();
        }

        public override void Dispose()
        {
            pipeServer.Dispose();
            sessionRunner.Dispose();
            emulatorJsRuntimeInstaller.Dispose();
            jsDosRuntimeInstaller.Dispose();
            scummVmRuntimeInstaller.Dispose();
            base.Dispose();
        }

        private LaunchResponse HandleLaunch(LaunchRequest request)
        {
            try
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                var profile = catalog.Get(request.ProfileId);
                if (string.IsNullOrWhiteSpace(request.RomPath))
                {
                    throw new InvalidDataException("The launcher did not provide a game file path.");
                }

                sessionRunner.Run(profile, request.RomPath);
                return LaunchResponse.Success();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Web Emulator launch failed.");
                if (!(exception is OperationCanceledException))
                {
                    PlayniteApi.Notifications.Add(
                        "playnite-web-emulator-launch",
                        $"Web Emulator could not launch the game: {exception.GetBaseException().Message}",
                        NotificationType.Error);
                }
                return LaunchResponse.Failure(exception.GetBaseException().Message);
            }
        }
    }
}
