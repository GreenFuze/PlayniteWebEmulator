# Third-party credits and license notices

Web Emulator is licensed under Apache-2.0. Emulator frontends, cores, and
engines are independent works and are not relicensed by this project. The
Playnite add-on package does not contain the emulator runtime binaries listed
below: the installed add-on downloads pinned, hash-verified artifacts directly
from the stated upstream projects on first use.

Web Emulator does not provide games, ROM images, copyrighted game data,
firmware, or BIOS files. Users must provide content they are legally entitled to
use.

## Browser emulator frontends and engines

| Component | Credit and source | License | How Web Emulator uses it |
| --- | --- | --- | --- |
| EmulatorJS 4.2.3 | [EmulatorJS contributors](https://github.com/EmulatorJS/EmulatorJS/tree/v4.2.3) | GPL-3.0 | Downloads the official `4.2.3.7z` release directly from GitHub; validates size `303,554,683` and SHA-256 `07d451bc06fa3ad04ab30d9b94eb63ac34ad0babee52d60357b002bde8f3850b`. The upstream archive includes its GPL license and all stable cores. |
| js-dos 8.3.20 | [Alexander Guryanov (caiiiycuk) and contributors](https://github.com/caiiiycuk/js-dos/tree/1263c31f0c4d1b3ed83cbb24b586c3d2e52a7228) | GPL-2.0 | Downloads the official `release.zip`; extracts only seven required frontend/backend files; installs the GPL-2.0 text and immutable source links beside the runtime; disables js-dos cloud and networking services. |
| js-dos emulator backend 8.3.8 | [caiiiycuk/emulators contributors](https://github.com/caiiiycuk/emulators/tree/387f7275010d529c408d9afe684584e6e18bd8c7) | GPL-2.0 | Supplies the pinned WebAssembly DOSBox backend used by js-dos. |
| DOSBox for js-dos | [DOSBox and js-dos contributors](https://github.com/js-dos/dosbox/tree/98d1639f66ec91652f5661cf2f4df689721a73e0) | GPL-2.0 | Corresponding source for the pinned js-dos DOSBox backend. |
| ScummVM | [ScummVM Team and contributors](https://github.com/scummvm/scummvm/tree/c663ad7ab10ad669c8b6d9941f1f3814ba4c2486) | GPL-3.0-or-later plus component licenses | Downloads individually pinned WebAssembly/runtime files and the Tinsel engine plug-in. The installed runtime includes `COPYING`, `COPYRIGHT`, `AUTHORS`, and all component license files supplied by the build. |
| scummvm-demo web build | [Christian Kuendig and contributors](https://github.com/chkuendig/scummvm-demo/tree/ccafc76bb8653da0987450599425b0f8d0fa125f) | Build source; bundled ScummVM terms apply | Immutable source and deployment used for the pinned Emscripten 6.0.2 browser build. |

## EmulatorJS cores used by Web Emulator profiles

EmulatorJS's official 4.2.3 release archive contains the complete stable core
set, including the cores below. Web Emulator selects these cores at runtime and
credits their authors and contributors. Follow each source link for the full
license text and component-level notices.

| Core | Platforms in Web Emulator | Credit and source | License / important restriction |
| --- | --- | --- | --- |
| FCEUmm | NES, Famicom Disk System | [FCEUmm and libretro contributors](https://github.com/libretro/libretro-fceumm) | GPL-2.0 |
| Snes9x | SNES | [Snes9x Team and libretro contributors](https://github.com/snes9xgit/snes9x) | Custom Snes9x license; **non-commercial use only** |
| Gambatte | Game Boy, Game Boy Color | [Sindre Aamås and libretro contributors](https://github.com/libretro/gambatte-libretro) | GPL-2.0 |
| mGBA | Game Boy Advance | [Jeffrey Pfau and contributors](https://github.com/mgba-emu/mgba) | MPL-2.0 |
| Mupen64Plus-Next | Nintendo 64 | [Mupen64Plus-Next and libretro contributors](https://github.com/libretro/mupen64plus-libretro-nx) | GPL-2.0 |
| PicoDrive | Sega Genesis/Mega Drive, Master System, Game Gear, Sega CD, Sega 32X | [notaz, fdave, and contributors](https://github.com/libretro/picodrive) | MAME-derived license; **non-commercial use only** |
| PCSX-ReARMed | PlayStation | [notaz and contributors](https://github.com/notaz/pcsx_rearmed) | GPL-2.0-or-later |
| MAME 2003-Plus | Arcade | [MAME and libretro contributors](https://github.com/libretro/mame2003-plus-libretro) | Classic MAME 0.78 license; **non-commercial use only**; users must provide lawful ROM images; binary source is available at the linked project |

The EmulatorJS core build pipeline is published at
[EmulatorJS/build](https://github.com/EmulatorJS/build). The exact official
runtime archive is pinned by URL, byte size, and SHA-256; this project neither
rebuilds nor modifies those core binaries.

## Build-time dependencies

| Component | Credit and source | License / distribution boundary |
| --- | --- | --- |
| Playnite SDK 6.16.0 | [Josef Nemec and Playnite contributors](https://github.com/JosefNemec/Playnite) | MIT; referenced only at build time, with the runtime assembly supplied by Playnite and excluded from the add-on package |
| SharpCompress 0.26.0 API | [SharpCompress contributors](https://github.com/adamhathcock/sharpcompress) | MIT; Playnite supplies the runtime assembly and it is excluded from the add-on package. Web Emulator does not call the advisory-affected `WriteToDirectory` helpers; it validates and writes every archive entry itself. |

## Provenance and safety gate

Every runtime manifest must record an official immutable artifact or file URL,
expected byte size, SHA-256, upstream source, and applicable license notices.
Missing provenance, a changed hash/size, an unsafe archive entry, or a missing
license file fails installation closed.

This document is an attribution and distribution record. It does not replace or
modify any upstream license.
