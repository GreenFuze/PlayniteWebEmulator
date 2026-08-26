using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace PlayniteWebEmulator.Emulation
{
    internal sealed class PlayniteEmulatorRegistrar
    {
        public static readonly Guid EmulatorId = Guid.Parse("d87293de-2a0b-4f57-ac35-657c5afda9fc");
        private const string EmulatorName = "Web Emulator";
        private const string ManagedProfilePrefix = "#custom_playnite-web-emulator-";

        private readonly IGameDatabase database;
        private readonly IEmulationAPI emulationApi;
        private readonly BrowserEmulatorProfileCatalog catalog;
        private readonly string pluginDirectory;

        public PlayniteEmulatorRegistrar(
            IGameDatabase database,
            IEmulationAPI emulationApi,
            BrowserEmulatorProfileCatalog catalog,
            string pluginDirectory)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.emulationApi = emulationApi ?? throw new ArgumentNullException(nameof(emulationApi));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.pluginDirectory = RequiredDirectory(pluginDirectory);
        }

        public Emulator Synchronize()
        {
            var launcherPath = Path.Combine(pluginDirectory, "PlayniteWebEmulator.Launcher.exe");
            if (!File.Exists(launcherPath))
            {
                throw new FileNotFoundException("The tracked Web Emulator launcher is missing.", launcherPath);
            }

            var emulator = database.Emulators.Get(EmulatorId);
            var isNew = emulator == null;
            if (isNew)
            {
                emulator = new Emulator
                {
                    Id = EmulatorId,
                    CustomProfiles = new ObservableCollection<CustomEmulatorProfile>(),
                    BuiltinProfiles = new ObservableCollection<BuiltInEmulatorProfile>()
                };
            }

            emulator.Name = EmulatorName;
            emulator.InstallDir = pluginDirectory;
            if (emulator.CustomProfiles == null)
            {
                emulator.CustomProfiles = new ObservableCollection<CustomEmulatorProfile>();
            }

            foreach (var descriptor in catalog.Profiles)
            {
                var platform = EnsurePlatform(descriptor);
                var profileId = ManagedProfilePrefix + descriptor.Id;
                var profile = emulator.CustomProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, profileId, StringComparison.Ordinal));
                if (profile == null)
                {
                    profile = new CustomEmulatorProfile { Id = profileId };
                    emulator.CustomProfiles.Add(profile);
                }

                profile.Name = descriptor.Name;
                profile.Platforms = new List<Guid> { platform.Id };
                profile.ImageExtensions = descriptor.ImageExtensions.ToList();
                profile.Executable = launcherPath;
                profile.Arguments = $"--profile \"{descriptor.Id}\" --rom \"{{ImagePath}}\"";
                profile.WorkingDirectory = pluginDirectory;
                profile.TrackingMode = TrackingMode.Process;
                profile.TrackingPath = launcherPath;
            }

            if (isNew)
            {
                database.Emulators.Add(new[] { emulator });
            }
            else
            {
                database.Emulators.Update(emulator);
            }

            return emulator;
        }

        private Platform EnsurePlatform(BrowserEmulatorProfile descriptor)
        {
            var knownPlatform = emulationApi.GetPlatform(descriptor.PlatformSpecificationId);
            if (knownPlatform == null)
            {
                throw new InvalidOperationException(
                    $"Playnite does not know platform specification '{descriptor.PlatformSpecificationId}'.");
            }

            var platform = database.Platforms.FirstOrDefault(candidate =>
                string.Equals(candidate.SpecificationId, descriptor.PlatformSpecificationId, StringComparison.OrdinalIgnoreCase));
            if (platform != null)
            {
                return platform;
            }

            platform = database.Platforms.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, knownPlatform.Name, StringComparison.OrdinalIgnoreCase));
            if (platform != null)
            {
                platform.SpecificationId = knownPlatform.Id;
                database.Platforms.Update(platform);
                return platform;
            }

            platform = new Platform(knownPlatform.Name) { SpecificationId = knownPlatform.Id };
            database.Platforms.Add(new[] { platform });
            return platform;
        }

        private static string RequiredDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("The plugin directory is required.", nameof(path));
            }

            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"The plugin directory does not exist: {fullPath}");
            }

            return fullPath;
        }
    }
}

