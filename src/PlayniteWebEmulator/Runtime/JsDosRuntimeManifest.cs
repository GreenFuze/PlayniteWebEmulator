using System.Collections.Generic;

namespace PlayniteWebEmulator.Runtime
{
    internal static class JsDosRuntimeManifest
    {
        public const string Version = "8.3.20";
        public const string SourceCommit = "1263c31f0c4d1b3ed83cbb24b586c3d2e52a7228";
        public const string EmulatorVersion = "8.3.8";
        public const string EmulatorCommit = "387f7275010d529c408d9afe684584e6e18bd8c7";
        public const string DosBoxCommit = "98d1639f66ec91652f5661cf2f4df689721a73e0";
        public const string ArchiveUrl = "https://github.com/caiiiycuk/js-dos/releases/download/v8.3.20/release.zip";
        public const long ArchiveSize = 3697001L;
        public const string ArchiveSha256 = "0ad8cc047c1a9beeeb508e2c09ce520da4b6df41019e93b09f84b4e6814824ef";
        public const string LicenseUrl = "https://raw.githubusercontent.com/caiiiycuk/emulators/v8.3.8/LICENSE";
        public const long LicenseSize = 18092L;
        public const string LicenseSha256 = "8177f97513213526df2cf6184d8ff986c675afb514d4e68a404010521b880643";

        public static readonly IReadOnlyList<RuntimeFile> RequiredFiles = new[]
        {
            new RuntimeFile("js-dos.css", 113994L, "fe68ac9154aff78ec3904cacaa6d680003cc7f112debb3f0157ed4b534f91023"),
            new RuntimeFile("js-dos.js", 308716L, "d6316862834616fb120e21616b88bab6b09c1f1a4dd6eaf2b6efa83b346cfa64"),
            new RuntimeFile("emulators/emulators.js", 71993L, "9637c08567c44c4ab1de982008a0710180f5e3ab84b05745fad3fcec945e97c8"),
            new RuntimeFile("emulators/wdosbox.js", 103385L, "d727efe319d99ceebf4cc8f4e6b392ed0bbb687e4e1878e1261d2f04d9e7b0ba"),
            new RuntimeFile("emulators/wdosbox.wasm", 1458714L, "6c3e68a2669cbde5a2b9c920e64248b15c23b38c0b6080814aff0542676b6e98"),
            new RuntimeFile("emulators/wlibzip.js", 74502L, "c19d0ce2ed8f686637e4abe54b374142c4d8092aa4471fd8153f97abf6436a88"),
            new RuntimeFile("emulators/wlibzip.wasm", 113081L, "cff5e8e1600ba7c589e43966613956060b4696924355714faf6b28e4c35db48f")
        };

        public static string SourceNotice =>
            "js-dos " + Version + "\r\n" +
            "Copyright Alexander Guryanov (aka caiiiycuk) and contributors.\r\n" +
            "License: GNU General Public License version 2.\r\n" +
            "Frontend source: https://github.com/caiiiycuk/js-dos/tree/" + SourceCommit + "\r\n" +
            "Emulator backend " + EmulatorVersion + " source: https://github.com/caiiiycuk/emulators/tree/" + EmulatorCommit + "\r\n" +
            "DOSBox source used by the backend: https://github.com/js-dos/dosbox/tree/" + DosBoxCommit + "\r\n" +
            "Official release: " + ArchiveUrl + "\r\n";
    }
}
