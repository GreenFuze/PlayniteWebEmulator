using PlayniteWebEmulator.Emulation;
using PlayniteWebEmulator.Launcher;
using PlayniteWebEmulator.Protocol;
using PlayniteWebEmulator.Hosting;
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
            var html = EmulatorJsPlayerPage.Build(profile, "Altered Beast");
            True(html.Contains("window.EJS_controlScheme='segaMD'"), "Genesis control scheme missing");
            True(html.Contains("input='./runtime/version.json'"), "local version redirect missing");
            True(html.Contains("report('fullscreen',enabled?'enter':'exit')"), "fullscreen bridge missing");
        }

        private static void CommandLineParses()
        {
            var parsed = LaunchCommandLine.Parse(new[] { "--profile", "emulatorjs.nes", "--rom", @"C:\Games\Mario Bros.nes" });
            Equal("emulatorjs.nes", parsed.ProfileId, "profile");
            Equal(@"C:\Games\Mario Bros.nes", parsed.RomPath, "ROM");
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
