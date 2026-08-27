using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlayniteWebEmulator.Emulation
{
    internal sealed class ScummVmEngineResolver
    {
        public ScummVmLaunchPlan Resolve(string markerPath)
        {
            if (string.IsNullOrWhiteSpace(markerPath)) throw new ArgumentException("A ScummVM marker path is required.", nameof(markerPath));
            var fullMarkerPath = Path.GetFullPath(markerPath);
            if (!File.Exists(fullMarkerPath)) throw new FileNotFoundException("The ScummVM marker does not exist.", fullMarkerPath);
            if (!string.Equals(Path.GetExtension(fullMarkerPath), ".scummvm", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A ScummVM launch must use a .scummvm marker.");

            var gameRoot = Path.GetDirectoryName(fullMarkerPath);
            if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
                throw new DirectoryNotFoundException("The ScummVM game directory does not exist.");

            var files = Directory.GetFiles(gameRoot, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, fullMarkerPath, StringComparison.OrdinalIgnoreCase))
                .Where(path => !string.Equals(Path.GetFileName(path), ".cloud-storage-install.json", StringComparison.OrdinalIgnoreCase))
                .Select(path => new ScummVmGameFile(gameRoot, path))
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0) throw new InvalidDataException("The ScummVM game directory contains no game files.");

            var names = new HashSet<string>(files.Select(file => Path.GetFileName(file.RelativePath)), StringComparer.OrdinalIgnoreCase);
            var directoryName = Path.GetFileName(gameRoot) ?? string.Empty;
            if (directoryName.IndexOf("Discworld", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (names.Contains("DISCMAP.SCN") && names.Contains("OBJECTS.SCN")))
            {
                return new ScummVmLaunchPlan(gameRoot, "libtinsel.so", files);
            }

            throw new NotSupportedException(
                $"Web Emulator could not identify the ScummVM engine for '{directoryName}'. " +
                "This build currently pins the Tinsel engine used by Discworld.");
        }
    }

    internal sealed class ScummVmLaunchPlan
    {
        public string GameRoot { get; }
        public string EnginePluginFileName { get; }
        public IReadOnlyList<ScummVmGameFile> Files { get; }

        public ScummVmLaunchPlan(string gameRoot, string enginePluginFileName, IReadOnlyList<ScummVmGameFile> files)
        {
            GameRoot = gameRoot ?? throw new ArgumentNullException(nameof(gameRoot));
            EnginePluginFileName = enginePluginFileName ?? throw new ArgumentNullException(nameof(enginePluginFileName));
            Files = files ?? throw new ArgumentNullException(nameof(files));
            if (Files.Count == 0) throw new ArgumentException("At least one ScummVM game file is required.", nameof(files));
        }
    }

    internal sealed class ScummVmGameFile
    {
        public string RelativePath { get; }
        public long Size { get; }

        public ScummVmGameFile(string gameRoot, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(gameRoot)) throw new ArgumentException("A game root is required.", nameof(gameRoot));
            if (string.IsNullOrWhiteSpace(fullPath)) throw new ArgumentException("A game file path is required.", nameof(fullPath));
            var root = EnsureTrailingSeparator(Path.GetFullPath(gameRoot));
            var file = Path.GetFullPath(fullPath);
            if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A ScummVM game file escapes the game root.");
            if (!File.Exists(file)) throw new FileNotFoundException("A ScummVM game file is missing.", file);
            RelativePath = file.Substring(root.Length).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(RelativePath) || RelativePath.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidDataException("A ScummVM game file has an unsafe relative path.");
            Size = new FileInfo(file).Length;
        }

        private static string EnsureTrailingSeparator(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
}
