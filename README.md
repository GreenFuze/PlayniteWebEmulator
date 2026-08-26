# Web Emulator for Playnite

Web Emulator is a Playnite generic plugin that makes browser-based emulators
behave like normal Playnite emulators. It is being built for a no-manual-setup
flow: install the add-on, choose a compatible Web Emulator profile, and let the
plugin acquire the required runtime under its own managed data directory.

The repository is private while the first usable version is developed.

## Current status

The first vertical slice is under construction. The plugin currently defines
the managed Playnite emulator/profile catalog and a tracked launcher-to-plugin
session bridge. The initial diagnostic player proves the lifecycle before any
large third-party runtime is downloaded or redistributed.

Planned launch coverage matches the browser engines already proven in
MyGamesAnywhere (MGA):

| Engine | Playnite platforms |
| --- | --- |
| EmulatorJS | NES, SNES, Game Boy, Game Boy Color, Game Boy Advance, Nintendo 64, Sega Genesis/Mega Drive, Master System, Game Gear, Sega CD, Sega 32X, PlayStation, Arcade |
| js-dos | PC (DOS) |
| ScummVM WebAssembly | PC (DOS) and PC (Windows) ScummVM-compatible game data |

Arcade is intentionally implemented through EmulatorJS's MAME 2003-Plus core,
as in MGA. There is no separate `mame-js` runtime in MGA's current code.

## Design boundaries

- Web Emulator is a Playnite `GenericPlugin`, not a library/source plugin.
- It registers one managed emulator with ordinary custom Playnite profiles.
- A small helper process preserves Playnite's normal emulator process tracking.
- The loaded plugin hosts the player in Playnite's Chromium web view.
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

