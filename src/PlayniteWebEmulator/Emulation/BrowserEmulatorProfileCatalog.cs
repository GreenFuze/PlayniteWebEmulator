using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteWebEmulator.Emulation
{
    internal sealed class BrowserEmulatorProfileCatalog
    {
        private readonly IReadOnlyList<BrowserEmulatorProfile> profiles;

        public IReadOnlyList<BrowserEmulatorProfile> Profiles => profiles;

        public BrowserEmulatorProfileCatalog()
        {
            profiles = new[]
            {
                Ejs("nes", "NES", "nintendo_nes", "Nintendo Entertainment System", new[] { "nes", "fds", "zip" }, true),
                Ejs("snes", "SNES", "nintendo_super_nes", "Nintendo SNES", new[] { "smc", "sfc", "zip" }, true),
                Ejs("gb", "Game Boy", "nintendo_gameboy", "Nintendo Game Boy", new[] { "gb", "zip" }, true),
                Ejs("gbc", "Game Boy Color", "nintendo_gameboycolor", "Nintendo Game Boy Color", new[] { "gbc", "zip" }, true),
                Ejs("gba", "Game Boy Advance", "nintendo_gameboyadvance", "Nintendo Game Boy Advance", new[] { "gba", "zip" }, true),
                Ejs("n64", "Nintendo 64", "nintendo_64", "Nintendo 64", new[] { "n64", "z64", "v64", "zip" }, true),
                Ejs("genesis", "Sega Genesis / Mega Drive", "sega_genesis", "Sega Genesis", new[] { "gen", "md", "smd", "bin", "zip" }, true),
                Ejs("mastersystem", "Sega Master System", "sega_mastersystem", "Sega Master System", new[] { "sms", "zip" }, true),
                Ejs("gamegear", "Sega Game Gear", "sega_gamegear", "Sega Game Gear", new[] { "gg", "zip" }, true),
                Ejs("segacd", "Sega CD", "sega_cd", "Sega CD", new[] { "cue", "chd", "iso" }, true),
                Ejs("sega32x", "Sega 32X", "sega_32x", "Sega 32X", new[] { "32x", "bin", "zip" }, true),
                Ejs("ps1", "Sony PlayStation", "sony_playstation", "Sony PlayStation", new[] { "cue", "chd", "pbp", "iso" }, true),
                Ejs("arcade", "Arcade (MAME 2003-Plus)", "arcade", "Arcade", new[] { "zip", "7z" }, false),
                Profile("jsdos.dos", "PC (DOS) — js-dos", "jsdos", "pc_dos", "PC (DOS)", new[] { "jsdos", "zip", "exe", "com", "bat" }, false),
                Profile("scummvm.dos", "PC (DOS) — ScummVM", "scummvm", "pc_dos", "PC (DOS)", new[] { "scummvm", "zip" }, false),
                Profile("scummvm.windows", "PC (Windows) — ScummVM", "scummvm", "pc_windows", "PC (Windows)", new[] { "scummvm", "zip" }, false)
            };

            var duplicate = profiles.GroupBy(profile => profile.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new InvalidOperationException($"Duplicate Web Emulator profile ID '{duplicate.Key}'.");
            }
        }

        public BrowserEmulatorProfile Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A profile ID is required.", nameof(id));
            }

            return profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException($"Unknown Web Emulator profile '{id}'.");
        }

        private static BrowserEmulatorProfile Ejs(
            string id,
            string name,
            string platformSpecificationId,
            string platformName,
            IEnumerable<string> imageExtensions,
            bool supportsRetroAchievements) =>
            Profile("emulatorjs." + id, name + " — EmulatorJS", "emulatorjs", platformSpecificationId, platformName, imageExtensions, supportsRetroAchievements);

        private static BrowserEmulatorProfile Profile(
            string id,
            string name,
            string runtimeId,
            string platformSpecificationId,
            string platformName,
            IEnumerable<string> imageExtensions,
            bool supportsRetroAchievements) =>
            new BrowserEmulatorProfile(id, name, runtimeId, platformSpecificationId, platformName, imageExtensions, supportsRetroAchievements);
    }
}

