# Web Emulator for Playnite

Web Emulator is a Playnite generic plugin that makes browser-based emulators
behave like normal Playnite emulators. It is being built for a no-manual-setup
flow: install the add-on, choose a compatible Web Emulator profile, and let the
plugin acquire the required runtime under its own managed data directory.

The repository is private while the first usable version is developed.

## Current status

The first playable vertical slices are available for development testing.
EmulatorJS profiles launch cartridge and arcade games in the user's default
browser. ScummVM launches the DOS CD release of Discworld through its pinned
Tinsel engine. Each implemented runtime is downloaded on first use, verified
by size and SHA-256, and kept in the plugin's managed data directory. js-dos
and unpinned ScummVM engines deliberately fail fast.

Planned launch coverage matches the browser engines already proven in
MyGamesAnywhere (MGA):

| Engine | Playnite platforms |
| --- | --- |
| EmulatorJS | NES, SNES, Game Boy, Game Boy Color, Game Boy Advance, Nintendo 64, Sega Genesis/Mega Drive, Master System, Game Gear, Sega CD, Sega 32X, PlayStation, Arcade |
| js-dos | PC (DOS) |
| ScummVM WebAssembly | PC (DOS) and PC (Windows) ScummVM-compatible game data; Tinsel/Discworld is the first pinned engine |

Arcade is intentionally implemented through EmulatorJS's MAME 2003-Plus core,
as in MGA. There is no separate `mame-js` runtime in MGA's current code.

## Design boundaries

- Web Emulator is a Playnite `GenericPlugin`, not a library/source plugin.
- It registers one managed emulator with ordinary custom Playnite profiles.
- A small helper process preserves Playnite's normal emulator process tracking.
- The loaded plugin serves the player on loopback and opens it in the user's
  default browser, where native fullscreen and browser acceleration work.
- The helper remains alive while the browser tab reports its session, so
  Playnite still tracks running state and playtime.
- Emulator runtimes are acquired per engine, pinned, hash-verified, and stored
  beneath the plugin's user-data directory.
- ROMs, game data, firmware, and BIOS files are never supplied by this project.
- Runtime-specific save states and RetroAchievements are capabilities, not
  assumptions; the UI will state what each selected engine supports.

See [the first architecture decision](docs/architecture/0001-managed-web-emulator.md)
and [third-party notices](THIRD_PARTY_NOTICES.md).

## Build

Requirements:

- Windows
- .NET SDK capable of targeting .NET Framework 4.6.2
- Playnite 10 SDK (restored from NuGet)

```powershell
dotnet build PlayniteWebEmulator.sln
```

The plugin is licensed under Apache-2.0. Third-party emulator runtimes and
cores retain their own licenses; consult `THIRD_PARTY_NOTICES.md` before
redistributing a runtime cache.
