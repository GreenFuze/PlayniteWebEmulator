# Third-party notices and runtime gate

Web Emulator gives visible credit to every emulator frontend, core, and engine
it uses. It does not provide ROMs, copyrighted game data, firmware, or BIOS
files. Users must provide game data they are legally entitled to use.

No third-party runtime binary is committed to this repository. Approved
runtimes are downloaded on demand only after passing the provenance gate below.

| Component | Intended pinned line | Upstream and credit | License / constraint | Distribution plan |
| --- | --- | --- | --- | --- |
| EmulatorJS | 4.2.3 initially, matching MGA | [EmulatorJS](https://github.com/EmulatorJS/EmulatorJS), created and maintained by the EmulatorJS contributors | GPL-3.0; individual libretro cores have their own licenses | Download the official release on demand; retain license and core notices |
| js-dos | 8.3.20 (`1263c31f0c4d1b3ed83cbb24b586c3d2e52a7228`), with emulator backend 8.3.8 (`387f7275010d529c408d9afe684584e6e18bd8c7`) and DOSBox (`98d1639f66ec91652f5661cf2f4df689721a73e0`) | [js-dos](https://github.com/caiiiycuk/js-dos), by Alexander Guryanov (aka caiiiycuk) and contributors; [DOSBox](https://github.com/js-dos/dosbox) | GPL-2.0; exact license text and immutable corresponding-source links are retained beside the downloaded runtime | Download official `v8.3.20/release.zip` on demand; require size `3,697,001` and SHA-256 `0ad8cc047c1a9beeeb508e2c09ce520da4b6df41019e93b09f84b4e6814824ef`; extract only the seven required frontend/DOSBox files (not the unused DOSBox-X backend); disable js-dos cloud/network services |
| ScummVM WebAssembly | ScummVM `c663ad7ab10ad669c8b6d9941f1f3814ba4c2486`, built with Emscripten 6.0.2; deployment `ccafc76bb8653da0987450599425b0f8d0fa125f` from [chkuendig/scummvm-demo](https://github.com/chkuendig/scummvm-demo) | [ScummVM](https://github.com/scummvm/scummvm), by the ScummVM Team; browser build maintained by the scummvm-demo contributors | GPL-3.0-or-later plus component licenses; the downloaded runtime includes `COPYING`, `COPYRIGHT`, `AUTHORS`, and all bundled component license files | Download immutable, individually SHA-256-pinned files on demand; currently install only the Tinsel engine plug-in needed by Discworld; corresponding source is the pinned ScummVM commit and demo build source |
| MAME 2003-Plus libretro core | EmulatorJS core, initially matching MGA | [MAME 2003-Plus](https://github.com/libretro/mame2003-plus-libretro), by the MAME and libretro contributors | Classic MAME 0.78 non-commercial license; binary distribution requires source availability and unchanged notices | Optional Arcade runtime component with an explicit non-commercial notice and source link |

## Provenance gate

Before adding a runtime manifest, all of the following must be recorded:

1. Exact upstream repository and immutable tag or commit.
2. Exact official artifact URL or reproducible build instructions.
3. SHA-256 for every downloaded archive.
4. Complete licenses/notices for the frontend and selected core(s).
5. Corresponding-source location when a copyleft binary is distributed.
6. A user-visible credit entry and capability/limitation entry.

Fail closed: a missing hash, unknown source revision, or absent license blocks
runtime installation.
