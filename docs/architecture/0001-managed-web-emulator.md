# 0001: Managed Web Emulator

Status: accepted for the first vertical slice

## Decision

Web Emulator is a Playnite GenericPlugin that registers one stable emulator
record with managed custom profiles. It does not pretend to be a source plugin
and does not own game discovery or installation.

Each profile starts `PlayniteWebEmulator.Launcher.exe`. The launcher sends a
validated request over a local named pipe and remains alive until the player
tab closes. The plugin opens the user's default browser and serves the player
plus game/runtime files from an ephemeral loopback-only HTTP endpoint. The page
reports closure explicitly and sends a low-frequency heartbeat so Playnite's
running state can fail closed if a browser exits without an unload event.

This arrangement preserves normal Playnite semantics:

- games retain ordinary emulator actions and can change emulator/profile;
- Cloud Storage can discover the profiles through Playnite's emulator model;
- Playnite tracks the helper process for playtime and running state;
- no browser extension or separately installed emulator is required;
- native browser fullscreen and cross-origin isolation remain available to
  EmulatorJS, avoiding Playnite web-view rendering limitations.

## Profile catalog

The catalog is explicit and fail-fast. A profile declares one runtime, one
Playnite platform specification ID, and supported image extensions. Unknown
profiles, missing ROMs, missing runtime manifests, failed checksums, or a dead
plugin pipe abort before opening a player.

PC (DOS) deliberately exposes both js-dos and ScummVM profiles. The engines
solve different problems: js-dos boots DOS executables/bundles, while ScummVM
interprets supported adventure-game data. The user may choose or later change
the profile in Playnite.

Arcade uses EmulatorJS with the MAME 2003-Plus libretro core. The older
`mame-js` label is not a separate runtime in current MGA. Its non-commercial
license is surfaced separately and Arcade remains an optional component.

## Runtime distribution

The add-on package stays small and contains only GreenFuze code, player glue,
licenses, and manifests. Third-party runtimes are downloaded into Playnite's
plugin user-data directory on first use. Manifests pin immutable artifacts and
SHA-256 values. Downloads are staged, verified, and atomically promoted.

Archive extraction does not use SharpCompress's `WriteToDirectory` helper.
Every entry is normalized and checked against the managed destination before
it is written; links, split/encrypted entries, duplicates, excessive entry
counts, and excessive expanded sizes are rejected. This is intentional because
Playnite 10 currently supplies SharpCompress 0.26 and newer advisories affect
the convenience extraction APIs.

No runtime may be added by copying an untraceable binary from MGA. MGA's launch
algorithms and Apache-licensed player glue may be adapted, but every emulator
binary must independently satisfy the provenance gate in
`THIRD_PARTY_NOTICES.md`.

## RetroAchievements and saves

The profile capability model will distinguish normal mode from a future
RetroAchievements-enabled mode. The plugin will not claim achievements,
save-RAM, snapshots, or cross-device save support unless the selected runtime
and core implement and pass an end-to-end test for that capability.
