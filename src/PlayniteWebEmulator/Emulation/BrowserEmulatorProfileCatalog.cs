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
                Ejs("nes", "NES", "fceumm", "nes", "nintendo_nes", "Nintendo Entertainment System", new[] { "nes", "fds", "zip" }, true),
                Ejs("snes", "SNES", "snes9x", "snes", "nintendo_super_nes", "Nintendo SNES", new[] { "smc", "sfc", "zip" }, true),
                Ejs("gb", "Game Boy", "gambatte", "gb", "nintendo_gameboy", "Nintendo Game Boy", new[] { "gb", "zip" }, true),
                Ejs("gbc", "Game Boy Color", "gambatte", "gb", "nintendo_gameboycolor", "Nintendo Game Boy Color", new[] { "gbc", "zip" }, true),
                Ejs("gba", "Game Boy Advance", "mgba", "gba", "nintendo_gameboyadvance", "Nintendo Game Boy Advance", new[] { "gba", "zip" }, true),
                Ejs("n64", "Nintendo 64", "mupen64plus_next", "n64", "nintendo_64", "Nintendo 64", new[] { "n64", "z64", "v64", "zip" }, true),
                Ejs("genesis", "Sega Genesis / Mega Drive", "picodrive", "segaMD", "sega_genesis", "Sega Genesis", new[] { "gen", "md", "smd", "bin", "zip" }, true),
                Ejs("mastersystem", "Sega Master System", "picodrive", "segaMS", "sega_mastersystem", "Sega Master System", new[] { "sms", "zip" }, true),
                Ejs("gamegear", "Sega Game Gear", "picodrive", "segaGG", "sega_gamegear", "Sega Game Gear", new[] { "gg", "zip" }, true),
                Ejs("segacd", "Sega CD", "picodrive", "segaCD", "sega_cd", "Sega CD", new[] { "cue", "chd", "iso" }, true),
                Ejs("sega32x", "Sega 32X", "picodrive", "sega32x", "sega_32x", "Sega 32X", new[] { "32x", "bin", "zip" }, true),
                Ejs("ps1", "Sony PlayStation", "pcsx_rearmed", "psx", "sony_playstation", "Sony PlayStation", new[] { "cue", "chd", "pbp", "iso" }, true),
                Ejs("arcade", "Arcade (MAME 2003-Plus)", "mame2003_plus", "mame", "arcade", "Arcade", new[] { "zip", "7z" }, false),
                Profile("jsdos.dos", "PC (DOS) — js-dos", "jsdos", null, null, "pc_dos", "PC (DOS)", new[] { "jsdos", "zip", "exe", "com", "bat" }, false),
                Profile("scummvm.dos", "PC (DOS) — ScummVM", "scummvm", null, null, "pc_dos", "PC (DOS)", new[] { "scummvm", "zip" }, false),
                Profile("scummvm.windows", "PC (Windows) — ScummVM", "scummvm", null, null, "pc_windows", "PC (Windows)", new[] { "scummvm", "zip" }, false)
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
            string coreId,
            string controlSchemeId,
            string platformSpecificationId,
            string platformName,
            IEnumerable<string> imageExtensions,
            bool supportsRetroAchievements) =>
            Profile("emulatorjs." + id, name + " — EmulatorJS", "emulatorjs", coreId, controlSchemeId, platformSpecificationId, platformName, imageExtensions, supportsRetroAchievements);

        private static BrowserEmulatorProfile Profile(
            string id,
            string name,
            string runtimeId,
            string coreId,
            string controlSchemeId,
            string platformSpecificationId,
            string platformName,
            IEnumerable<string> imageExtensions,
            bool supportsRetroAchievements) =>
            new BrowserEmulatorProfile(id, name, runtimeId, coreId, controlSchemeId, platformSpecificationId, platformName, imageExtensions, supportsRetroAchievements);
    }
}
