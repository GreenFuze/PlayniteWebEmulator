using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteWebEmulator.Runtime
{
    internal static class ScummVmRuntimeManifest
    {
        public const string DeploymentCommit = "ccafc76bb8653da0987450599425b0f8d0fa125f";
        public const string SourceCommit = "4880b36348d092a208fcd8ca764938fa2a205b24";
        public const string ScummVmCommit = "c663ad7ab10ad669c8b6d9941f1f3814ba4c2486";
        public const string EmscriptenVersion = "6.0.2";
        public const string BaseUrl = "https://raw.githubusercontent.com/chkuendig/scummvm-demo/" + DeploymentCommit + "/";

        private static readonly IReadOnlyList<RuntimeFile> BaseFiles = new[]
        {
            new RuntimeFile("AUTHORS", 42291L, "3110f0d3ce0b272a96338d49fcada68b99bdec2933fadad74b21ef0b10d3f40a"),
            new RuntimeFile("CatharonLicense.txt", 5359L, "372251a3ce2ecdb74e076dede2c7d0d1e16a5003c548177e9908627192dd064c"),
            new RuntimeFile("COPYING", 35149L, "3972dc9744f6499f0f9b2dbf76696f2ae7ad8af9b23dde66d6af86c9dfb36986"),
            new RuntimeFile("COPYING.Apache", 11488L, "314ca6233f2849131fc87c24cdf112aa204605051d8f17fbf5982945e34dbb67"),
            new RuntimeFile("COPYING.BSD", 8058L, "80bec7489af9bdd2e0f5467f6267f8985aaaca50e5061c9d02467e103c126628"),
            new RuntimeFile("COPYING.BSL", 1429L, "58afb430442855628fa2509cddd905ad61c12fe564731466f56554edd5344ae5"),
            new RuntimeFile("COPYING.GLAD", 3122L, "d74397819a7776231c638e73b9197a45faad8347278bdfec30a041d1cb808313"),
            new RuntimeFile("COPYING.ISC", 967L, "4bdb0a32d3ba3070ed77dc48efb5348ce647bac3a1716992fa4cc080b99a3af7"),
            new RuntimeFile("COPYING.LGPL", 26721L, "f2c0f033c76185c3c981ad458ea433ca2787ab9b6f4623cab500fc8024db0e05"),
            new RuntimeFile("COPYING.LUA", 2035L, "8e0272ae02203977f9d0312bd82fe2498f37741b51c52731b9af0d66dbde7b6a"),
            new RuntimeFile("COPYING.MIT", 2695L, "acc32538625eb6a7b2bdd43749a787efe9544400e10e5c01fb6dd91a1a60b94b"),
            new RuntimeFile("COPYING.MKV", 1600L, "cfac47baf5d584bc5458c90b33116087dd1bda638adf751f123e7d176e4ac67f"),
            new RuntimeFile("COPYING.MPL", 16842L, "c92198047ac07252eada8d895a9cbe9473504d54b28e7646b5fb053f876b9fc2"),
            new RuntimeFile("COPYING.OFL", 5067L, "a79c445b3f3061588468e44bc9fff8b8ebac169374fabb482ea7274a478b45b5"),
            new RuntimeFile("COPYING.TINYGL", 1324L, "f62a84ca5a9a1a6f36d63336c461b6d21902fdfa84a50969b0d9ec6c6f0b90d3"),
            new RuntimeFile("COPYRIGHT", 15302L, "95c3a8ed18246d6a7135d7ba0aacd4df9a0957f7e1de8f58ec8e011d84557a6b"),
            new RuntimeFile("data/gui-icons.dat", 3315855L, "e772a8bb64ad3d7a807e859ad55c6b41269e66e32dfc78cdb4e583e183b5d828"),
            new RuntimeFile("data/scummremastered.zip", 93833L, "07a382f7fb56fdcb45aa37e33b51d8cce57317b8b2269658f21d3e5dc67bd916"),
            new RuntimeFile("data/translations.dat", 3693914L, "88e4a57aa5d58b213f6a066518eb8a1cb1046132770987cc7dd8b06ce80e0156"),
            new RuntimeFile("scummvm.js", 9556310L, "505a7058c6d98032cb91045f57582f85635951ce1fe2ad43d04b7769d596f309"),
            new RuntimeFile("scummvm.wasm", 32542385L, "a31d2162bd4032bc475fe4fe90a86805a3f8750ea6d9b4fdde4321995f9489fa")
        };

        private static readonly IReadOnlyDictionary<string, RuntimeFile> EngineFiles =
            new Dictionary<string, RuntimeFile>(StringComparer.OrdinalIgnoreCase)
            {
                ["libtinsel.so"] = new RuntimeFile(
                    "data/plugins/libtinsel.so",
                    2131147L,
                    "96cfc8b5b3ebe18e4b5a15205da305aafe0c837b1cbea43290d63631e0949ce2")
            };

        public static IReadOnlyList<RuntimeFile> GetRequiredFiles(string enginePluginFileName)
        {
            if (string.IsNullOrWhiteSpace(enginePluginFileName))
                throw new ArgumentException("A ScummVM engine plugin is required.", nameof(enginePluginFileName));
            if (!EngineFiles.TryGetValue(enginePluginFileName.Trim(), out var engineFile))
                throw new NotSupportedException($"ScummVM engine plugin '{enginePluginFileName}' is not pinned yet.");
            return BaseFiles.Concat(new[] { engineFile }).ToList();
        }
    }

    internal sealed class RuntimeFile
    {
        public string RelativePath { get; }
        public long Size { get; }
        public string Sha256 { get; }

        public RuntimeFile(string relativePath, long size, string sha256)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("A runtime path is required.", nameof(relativePath));
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64) throw new ArgumentException("A SHA-256 hash is required.", nameof(sha256));
            RelativePath = relativePath.Replace('\\', '/').Trim('/');
            Size = size;
            Sha256 = sha256.ToLowerInvariant();
        }
    }
}
