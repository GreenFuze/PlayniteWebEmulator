# Web Emulator third-party runtime notice

Web Emulator itself is licensed under Apache-2.0. It does not bundle emulator
runtimes, games, ROM images, firmware, or BIOS files in the Playnite add-on
package.

When an emulation profile is first used, Web Emulator downloads a pinned,
hash-verified runtime directly from the runtime project's public upstream
release or source repository. Those independent components remain governed by
their own licenses. Full credits, source links, and license summaries are in
the [third-party notices](https://github.com/GreenFuze/PlayniteWebEmulator/blob/master/THIRD_PARTY_NOTICES.md).

In particular, the upstream licenses for Snes9x, PicoDrive, and MAME 2003-Plus
limit those cores to non-commercial use. Do not use those cores commercially.

By installing Web Emulator, you acknowledge that:

- third-party runtimes are downloaded on first use and stored in Playnite's
  extension-data directory;
- third-party license terms apply independently from Web Emulator's license;
- you are responsible for using only game data, ROMs, firmware, and BIOS files
  that you are legally entitled to use; and
- emulator and game compatibility is not guaranteed.

This notice is informational and does not replace or alter any upstream
license.
