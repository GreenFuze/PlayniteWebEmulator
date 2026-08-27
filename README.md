# Web Emulator for Playnite

Web Emulator is a Playnite generic add-on that makes browser-based emulators
behave like ordinary Playnite emulators. Install the add-on, assign a compatible
Web Emulator profile to a game, and let the plugin acquire the required runtime
under Playnite's managed extension-data directory.

## Current status

Version 0.1.0 is a public beta with three playable runtime paths:

- **EmulatorJS 4.2.3** for supported cartridge, disc, and MAME 2003-Plus
  arcade content;
- **js-dos 8.3.20** for installed DOS directories, including DOSBox
  `[autoexec]` discovery and user selection when launchers are ambiguous; and
- **ScummVM WebAssembly** with the pinned Tinsel engine used by Discworld.

The add-on opens each player in the user's default browser, where native
fullscreen and browser acceleration are available. A tracked helper process and
loopback-only server preserve Playnite's running state and playtime tracking.

| Runtime | Playnite platforms |
| --- | --- |
| EmulatorJS | NES, SNES, Game Boy, Game Boy Color, Game Boy Advance, Nintendo 64, Sega Genesis/Mega Drive, Master System, Game Gear, Sega CD, Sega 32X, PlayStation, Arcade |
| js-dos | PC (DOS) |
| ScummVM WebAssembly | PC (DOS) and PC (Windows); Tinsel/Discworld is the first pinned engine |

Arcade uses EmulatorJS's MAME 2003-Plus libretro core. There is no separate
`mame-js` runtime in MGA's current implementation.

## Runtime and game-data boundaries

- The Playnite `.pext` package contains only GreenFuze code, the tracked helper,
  release metadata, and license/credit documents.
- Emulator runtimes are downloaded directly from pinned upstream artifacts on
  first use, verified by size and SHA-256, and stored below Playnite's
  extension-data directory.
- EmulatorJS's official 4.2.3 archive is approximately 304 MB and contains its
  complete stable core set. js-dos and ScummVM are acquired separately.
- No games, ROMs, copyrighted game data, firmware, or BIOS files are supplied or
  downloaded by this project.
- js-dos cloud and networking services are disabled; game files stay in the
  local loopback session and are not uploaded by this add-on.
- Save states, persistent saves, and RetroAchievements depend on the selected
  runtime/core and are not guaranteed by this beta.

## Credits and licenses

Web Emulator is Apache-2.0. The emulator runtimes and cores are independent
works under their own licenses. In particular, **Snes9x, PicoDrive, and MAME
2003-Plus are restricted to non-commercial use by their upstream licenses**.

The add-on includes a **Web Emulator → Third-party credits and licenses** menu
item in Playnite. The complete component list, author credits, source links, and
license boundaries are also recorded in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Installation is subject to the
[third-party runtime notice](USER_AGREEMENT.md).

Web Emulator reuses browser-emulation integration ideas from
[MyGamesAnywhere (MGA)](https://github.com/GreenFuze/MyGamesAnywhere), another
Apache-2.0 GreenFuze project. No untraceable emulator binary is copied from MGA.

## Build and package

Requirements:

- Windows
- .NET SDK capable of targeting .NET Framework 4.6.2
- Playnite 10 SDK (restored from NuGet)
- Playnite Toolbox for producing a `.pext` package

```powershell
dotnet build PlayniteWebEmulator.sln
dotnet run --project tests/PlayniteWebEmulator.Tests/PlayniteWebEmulator.Tests.csproj
./build-package.ps1 -ToolboxPath C:\path\to\Toolbox.exe
```

See [the architecture decision](docs/architecture/0001-managed-web-emulator.md)
for the plugin/runtime boundary and security model.
