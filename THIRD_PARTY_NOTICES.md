# Third-party notices and runtime gate

Web Emulator gives visible credit to every emulator frontend, core, and engine
it uses. It does not provide ROMs, copyrighted game data, firmware, or BIOS
files. Users must provide game data they are legally entitled to use.

No third-party runtime binary is committed to this repository at this stage.
Each runtime must pass the provenance gate below before the runtime manager is
allowed to download it.

| Component | Intended pinned line | Upstream and credit | License / constraint | Distribution plan |
| --- | --- | --- | --- | --- |
| EmulatorJS | 4.2.3 initially, matching MGA | [EmulatorJS](https://github.com/EmulatorJS/EmulatorJS), created and maintained by the EmulatorJS contributors | GPL-3.0; individual libretro cores have their own licenses | Download the official release on demand; retain license and core notices |
| js-dos | 8.3.20 initially, matching MGA | [js-dos](https://github.com/caiiiycuk/js-dos), by Andrey Kudryavtsev and contributors | GPL-2.0 family; bundled DOSBox/emulator artifacts require their own corresponding notices and source offer | Download a pinned official release on demand after exact artifact/license inventory |
| ScummVM WebAssembly | Exact MGA build is not yet accepted | [ScummVM](https://github.com/scummvm/scummvm), by the ScummVM Team | GPL-3.0-or-later plus component licenses; corresponding source/build provenance is mandatory | Rebuild from a pinned upstream revision or acquire a traceable official artifact; do not copy the opaque MGA binary |
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

