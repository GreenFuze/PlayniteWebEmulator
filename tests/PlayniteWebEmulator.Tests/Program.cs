using PlayniteWebEmulator.Emulation;
using PlayniteWebEmulator.Launcher;
using PlayniteWebEmulator.Protocol;
using PlayniteWebEmulator.Hosting;
using PlayniteWebEmulator.Runtime;
using System;
using System.IO;
using System.Linq;

namespace PlayniteWebEmulator.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("catalog covers MGA runtimes", CatalogCoversMgaRuntimes);
            Run("catalog IDs are unique", CatalogIdsAreUnique);
            Run("player page pins platform controls and local update data", PlayerPagePinsControlsAndLocalData);
            Run("ScummVM Discworld plan is explicit and safe", ScummVmDiscworldPlanIsExplicitAndSafe);
            Run("ScummVM player mounts game data and pinned engine", ScummVmPlayerMountsGameDataAndPinnedEngine);
            Run("command line parses quoted values supplied by Windows", CommandLineParses);
            Run("command line fails on missing ROM", CommandLineFailsOnMissingRom);
            Run("pipe protocol round trips", PipeProtocolRoundTrips);
            Console.WriteLine(failures == 0 ? "All tests passed." : $"{failures} test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void CatalogCoversMgaRuntimes()
        {
            var catalog = new BrowserEmulatorProfileCatalog();
            Equal(16, catalog.Profiles.Count, "profile count");
            True(catalog.Profiles.Any(profile => profile.RuntimeId == "emulatorjs"), "EmulatorJS missing");
            True(catalog.Profiles.Any(profile => profile.RuntimeId == "jsdos"), "js-dos missing");
            True(catalog.Profiles.Any(profile => profile.RuntimeId == "scummvm"), "ScummVM missing");
            Equal("arcade", catalog.Get("emulatorjs.arcade").PlatformSpecificationId, "Arcade platform");
            Equal("mame2003_plus", catalog.Get("emulatorjs.arcade").CoreId, "Arcade core");
            Equal("fceumm", catalog.Get("emulatorjs.nes").CoreId, "NES core");
            Equal("segaMD", catalog.Get("emulatorjs.genesis").ControlSchemeId, "Genesis controls");
            Equal("segaMS", catalog.Get("emulatorjs.mastersystem").ControlSchemeId, "Master System controls");
        }

        private static void CatalogIdsAreUnique()
        {
            var catalog = new BrowserEmulatorProfileCatalog();
            Equal(catalog.Profiles.Count, catalog.Profiles.Select(profile => profile.Id).Distinct().Count(), "unique IDs");
        }

        private static void PlayerPagePinsControlsAndLocalData()
        {
            var profile = new BrowserEmulatorProfileCatalog().Get("emulatorjs.genesis");
            var html = EmulatorJsPlayerPage.Build(profile, "Altered Beast", "Altered Beast (USA, Europe).zip");
            True(html.Contains("window.EJS_controlScheme='segaMD'"), "Genesis control scheme missing");
            True(html.Contains("window.EJS_gameUrl='./game/Altered%20Beast%20%28USA%2C%20Europe%29.zip'"), "ROM filename-preserving URL missing");
            True(html.Contains("input='./runtime/version.json'"), "local version redirect missing");
            True(html.Contains("window.EJS_threads=true"), "threaded browser runtime missing");
            True(html.Contains("navigator.sendBeacon('./diagnostics?event=closed"), "browser close tracking missing");
            True(!html.Contains("playniteFullscreen"), "Playnite fullscreen shim should not exist");

            var arcade = new BrowserEmulatorProfileCatalog().Get("emulatorjs.arcade");
            var arcadeHtml = EmulatorJsPlayerPage.Build(arcade, "armwar", "armwar.zip");
            True(arcadeHtml.Contains("window.EJS_gameName='armwar'"), "MAME set name missing");
            True(arcadeHtml.Contains("window.EJS_gameUrl='./game/armwar.zip'"), "MAME archive filename missing");
        }

        private static void CommandLineParses()
        {
            var parsed = LaunchCommandLine.Parse(new[] { "--profile", "emulatorjs.nes", "--rom", @"C:\Games\Mario Bros.nes" });
            Equal("emulatorjs.nes", parsed.ProfileId, "profile");
            Equal(@"C:\Games\Mario Bros.nes", parsed.RomPath, "ROM");
        }

        private static void ScummVmDiscworldPlanIsExplicitAndSafe()
        {
            var root = Path.Combine(Path.GetTempPath(), "playnite-web-emulator-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllBytes(Path.Combine(root, "DISCMAP.SCN"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(root, "OBJECTS.SCN"), new byte[] { 2 });
                var marker = Path.Combine(root, "launch.scummvm");
                File.WriteAllText(marker, string.Empty);
                var plan = new ScummVmEngineResolver().Resolve(marker);
                Equal("libtinsel.so", plan.EnginePluginFileName, "Discworld engine plugin");
                Equal(2, plan.Files.Count, "game file count");
                True(plan.Files.All(file => !file.RelativePath.Contains("..")), "unsafe relative path");
                True(ScummVmRuntimeManifest.GetRequiredFiles(plan.EnginePluginFileName)
                    .Any(file => file.RelativePath == "data/plugins/libtinsel.so"), "pinned Tinsel runtime missing");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void ScummVmPlayerMountsGameDataAndPinnedEngine()
        {
            var root = Path.Combine(Path.GetTempPath(), "playnite-web-emulator-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllBytes(Path.Combine(root, "DISCMAP.SCN"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(root, "OBJECTS.SCN"), new byte[] { 2 });
                var marker = Path.Combine(root, "launch.scummvm");
                File.WriteAllText(marker, string.Empty);
                var plan = new ScummVmEngineResolver().Resolve(marker);
                var html = ScummVmPlayerPage.Build("Discworld", plan);
                True(html.Contains("/plugins/libtinsel.so"), "Tinsel plugin mount missing");
                True(html.Contains("--path=/games/game --auto-detect"), "ScummVM auto-detect arguments missing");
                True(html.Contains("./game/DISCMAP.SCN"), "game route missing");
                True(html.Contains("id=\"download-modal-progress-fill\""), "ScummVM download progress contract missing");
                True(html.Contains("httpHideProgressBar"), "ScummVM progress completion hook missing");
                True(html.Contains("navigator.sendBeacon('./diagnostics?event=closed"), "close tracking missing");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void CommandLineFailsOnMissingRom()
        {
            Throws<ArgumentException>(() => LaunchCommandLine.Parse(new[] { "--profile", "emulatorjs.nes" }));
        }

        private static void PipeProtocolRoundTrips()
        {
            using (var buffer = new MemoryStream())
            {
                PipeProtocol.Write(buffer, new LaunchRequest { ProfileId = "emulatorjs.nes", RomPath = @"C:\game.nes" });
                buffer.Position = 0;
                var value = PipeProtocol.Read<LaunchRequest>(buffer);
                Equal("emulatorjs.nes", value.ProfileId, "protocol profile");
                Equal(@"C:\game.nes", value.RomPath, "protocol ROM");
            }
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name + ": " + exception.Message);
            }
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Equal<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
            }
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException($"Expected {typeof(T).Name}.");
        }
    }
}
