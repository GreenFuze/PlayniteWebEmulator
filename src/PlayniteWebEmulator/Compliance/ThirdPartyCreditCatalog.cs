using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayniteWebEmulator.Compliance
{
    internal sealed class ThirdPartyCredit
    {
        public string Name { get; }
        public string Authors { get; }
        public string License { get; }
        public string SourceUrl { get; }
        public IReadOnlyList<string> ComponentIds { get; }

        public ThirdPartyCredit(string name, string authors, string license, string sourceUrl, params string[] componentIds)
        {
            Name = name;
            Authors = authors;
            License = license;
            SourceUrl = sourceUrl;
            ComponentIds = componentIds ?? new string[0];
        }
    }

    internal sealed class ThirdPartyCreditCatalog
    {
        private readonly IReadOnlyList<ThirdPartyCredit> credits;

        public IReadOnlyList<ThirdPartyCredit> Credits => credits;

        public ThirdPartyCreditCatalog()
        {
            credits = new[]
            {
                new ThirdPartyCredit("EmulatorJS", "EmulatorJS contributors", "GPL-3.0", "https://github.com/EmulatorJS/EmulatorJS", "emulatorjs"),
                new ThirdPartyCredit("FCEUmm", "FCEUmm and libretro contributors", "GPL-2.0", "https://github.com/libretro/libretro-fceumm", "fceumm"),
                new ThirdPartyCredit("Snes9x", "Snes9x Team and libretro contributors", "Snes9x non-commercial license", "https://github.com/snes9xgit/snes9x", "snes9x"),
                new ThirdPartyCredit("Gambatte", "Sindre Aamås and libretro contributors", "GPL-2.0", "https://github.com/libretro/gambatte-libretro", "gambatte"),
                new ThirdPartyCredit("mGBA", "Jeffrey Pfau and contributors", "MPL-2.0", "https://github.com/mgba-emu/mgba", "mgba"),
                new ThirdPartyCredit("Mupen64Plus-Next", "Mupen64Plus-Next and libretro contributors", "GPL-2.0", "https://github.com/libretro/mupen64plus-libretro-nx", "mupen64plus_next"),
                new ThirdPartyCredit("PicoDrive", "notaz, fdave, and contributors", "MAME-derived non-commercial license", "https://github.com/libretro/picodrive", "picodrive"),
                new ThirdPartyCredit("PCSX-ReARMed", "notaz and contributors", "GPL-2.0-or-later", "https://github.com/notaz/pcsx_rearmed", "pcsx_rearmed"),
                new ThirdPartyCredit("MAME 2003-Plus", "MAME and libretro contributors", "MAME 0.78 non-commercial license", "https://github.com/libretro/mame2003-plus-libretro", "mame2003_plus"),
                new ThirdPartyCredit("js-dos", "Alexander Guryanov (caiiiycuk) and contributors", "GPL-2.0", "https://github.com/caiiiycuk/js-dos", "jsdos"),
                new ThirdPartyCredit("DOSBox for js-dos", "DOSBox and js-dos contributors", "GPL-2.0", "https://github.com/js-dos/dosbox"),
                new ThirdPartyCredit("ScummVM", "ScummVM Team and contributors", "GPL-3.0-or-later plus component licenses", "https://github.com/scummvm/scummvm", "scummvm"),
                new ThirdPartyCredit("scummvm-demo web build", "Christian Kuendig and contributors", "Build source; bundled ScummVM terms apply", "https://github.com/chkuendig/scummvm-demo")
            };
        }

        public bool CoversComponent(string componentId)
        {
            return !string.IsNullOrWhiteSpace(componentId) &&
                credits.Any(credit => credit.ComponentIds.Contains(componentId, System.StringComparer.Ordinal));
        }

        public string BuildDisplayText()
        {
            var text = new StringBuilder();
            text.AppendLine("Web Emulator is built by GreenFuze and licensed under Apache-2.0.");
            text.AppendLine();
            text.AppendLine("Emulator runtimes are independent projects downloaded from their upstream releases on first use. They are not part of Web Emulator's Apache-2.0 license.");
            text.AppendLine();
            foreach (var credit in credits)
            {
                text.AppendLine(credit.Name);
                text.AppendLine("  Credit: " + credit.Authors);
                text.AppendLine("  License: " + credit.License);
                text.AppendLine("  Source: " + credit.SourceUrl);
                text.AppendLine();
            }

            text.AppendLine("Important: Snes9x, PicoDrive, and MAME 2003-Plus are limited to non-commercial use by their upstream licenses.");
            text.AppendLine("Web Emulator supplies no games, ROMs, firmware, or BIOS files. Use only game data you are legally entitled to use.");
            return text.ToString().Trim();
        }
    }
}
