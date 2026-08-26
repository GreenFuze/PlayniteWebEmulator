using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteWebEmulator.Runtime
{
    internal sealed class EmulatorJsRuntimeInstaller : IDisposable
    {
        private const long MaximumExpandedBytes = 4L * 1024 * 1024 * 1024;
        private const int MaximumEntries = 20000;
        private readonly string runtimeRoot;
        private readonly HttpClient httpClient;
        private readonly SemaphoreSlim installationLock = new SemaphoreSlim(1, 1);

        public EmulatorJsRuntimeInstaller(string pluginUserDataPath)
        {
            if (string.IsNullOrWhiteSpace(pluginUserDataPath))
                throw new ArgumentException("The plugin user-data path is required.", nameof(pluginUserDataPath));
            runtimeRoot = Path.Combine(Path.GetFullPath(pluginUserDataPath), "runtimes", "emulatorjs");
            httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PlayniteWebEmulator/0.1.0");
        }

        public async Task<string> EnsureInstalledAsync(
            Action<RuntimeInstallProgress> reportProgress,
            CancellationToken cancellationToken)
        {
            await installationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var finalPath = Path.Combine(runtimeRoot, EmulatorJsRuntimeManifest.Version);
                if (TryFindRuntimeDataDirectory(finalPath, out var existingDataPath))
                {
                    reportProgress?.Invoke(new RuntimeInstallProgress("EmulatorJS is ready", 1, 1));
                    return existingDataPath;
                }

                Directory.CreateDirectory(runtimeRoot);
                if (Directory.Exists(finalPath))
                {
                    DeleteManagedDirectory(finalPath);
                }

                var installRoot = Path.Combine(runtimeRoot, ".install-" + Guid.NewGuid().ToString("N"));
                var contentRoot = Path.Combine(installRoot, "content");
                var archivePath = Path.Combine(installRoot, "EmulatorJS-" + EmulatorJsRuntimeManifest.Version + ".7z");
                Directory.CreateDirectory(contentRoot);
                try
                {
                    await DownloadAsync(archivePath, reportProgress, cancellationToken).ConfigureAwait(false);
                    Extract(archivePath, contentRoot, reportProgress, cancellationToken);
                    if (!TryFindRuntimeDataDirectory(contentRoot, out var stagedDataPath))
                    {
                        throw new InvalidDataException("The official EmulatorJS archive did not contain data/loader.js.");
                    }

                    RequireCore(stagedDataPath, "mame2003_plus");
                    File.WriteAllText(
                        Path.Combine(contentRoot, ".playnite-web-emulator-runtime"),
                        EmulatorJsRuntimeManifest.ArchiveSha256);
                    Directory.Move(contentRoot, finalPath);
                    if (!TryFindRuntimeDataDirectory(finalPath, out var installedDataPath))
                    {
                        throw new InvalidDataException("The installed EmulatorJS runtime could not be validated.");
                    }

                    return installedDataPath;
                }
                finally
                {
                    if (Directory.Exists(installRoot)) DeleteManagedDirectory(installRoot);
                }
            }
            finally
            {
                installationLock.Release();
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
            installationLock.Dispose();
        }

        private async Task DownloadAsync(
            string archivePath,
            Action<RuntimeInstallProgress> reportProgress,
            CancellationToken cancellationToken)
        {
            using (var response = await httpClient.GetAsync(
                EmulatorJsRuntimeManifest.ArchiveUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var declaredLength = response.Content.Headers.ContentLength;
                if (declaredLength.HasValue && declaredLength.Value != EmulatorJsRuntimeManifest.ArchiveSize)
                {
                    throw new InvalidDataException(
                        $"EmulatorJS archive size changed: expected {EmulatorJsRuntimeManifest.ArchiveSize}, got {declaredLength.Value}.");
                }

                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                using (var sha256 = SHA256.Create())
                {
                    var buffer = new byte[1024 * 1024];
                    long completed = 0;
                    int read;
                    reportProgress?.Invoke(new RuntimeInstallProgress("Downloading EmulatorJS 4.2.3", 0, EmulatorJsRuntimeManifest.ArchiveSize));
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                        sha256.TransformBlock(buffer, 0, read, null, 0);
                        completed += read;
                        reportProgress?.Invoke(new RuntimeInstallProgress(
                            "Downloading EmulatorJS 4.2.3",
                            completed,
                            EmulatorJsRuntimeManifest.ArchiveSize));
                    }

                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    if (completed != EmulatorJsRuntimeManifest.ArchiveSize)
                    {
                        throw new InvalidDataException(
                            $"EmulatorJS download is incomplete: expected {EmulatorJsRuntimeManifest.ArchiveSize} bytes, got {completed}.");
                    }

                    var actualHash = string.Concat(sha256.Hash.Select(value => value.ToString("x2")));
                    if (!string.Equals(actualHash, EmulatorJsRuntimeManifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"EmulatorJS SHA-256 mismatch. Expected {EmulatorJsRuntimeManifest.ArchiveSha256}, got {actualHash}.");
                    }
                }
            }
        }

        private static void Extract(
            string archivePath,
            string destinationRoot,
            Action<RuntimeInstallProgress> reportProgress,
            CancellationToken cancellationToken)
        {
            using (var archive = ArchiveFactory.Open(archivePath))
            {
                if (archive.Type != ArchiveType.SevenZip || !archive.IsComplete || archive.Volumes.Count() != 1)
                {
                    throw new InvalidDataException("The EmulatorJS package is not a complete single-volume 7z archive.");
                }

                var root = EnsureTrailingSeparator(Path.GetFullPath(destinationRoot));
                var entries = archive.Entries.ToList();
                if (entries.Count == 0 || entries.Count > MaximumEntries)
                    throw new InvalidDataException("The EmulatorJS archive entry count is invalid.");

                var targets = new Dictionary<string, ExtractionTarget>(StringComparer.Ordinal);
                long totalBytes = 0;
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.IsEncrypted || entry.IsSplitAfter || !string.IsNullOrWhiteSpace(entry.LinkTarget))
                        throw new InvalidDataException($"Unsupported EmulatorJS archive entry: {entry.Key}");
                    var key = NormalizeEntryKey(entry.Key);
                    if (key.Length == 0) continue;
                    var destination = Path.GetFullPath(Path.Combine(destinationRoot, key.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"EmulatorJS archive entry escapes the destination: {entry.Key}");
                    if (targets.ContainsKey(key))
                        throw new InvalidDataException($"Duplicate EmulatorJS archive entry: {entry.Key}");
                    targets.Add(key, new ExtractionTarget(destination, entry.IsDirectory, entry.Size));
                    if (!entry.IsDirectory)
                    {
                        checked { totalBytes += entry.Size; }
                        if (totalBytes > MaximumExpandedBytes)
                            throw new InvalidDataException("The EmulatorJS archive expands beyond the safety limit.");
                    }
                }

                long completed = 0;
                reportProgress?.Invoke(new RuntimeInstallProgress("Extracting EmulatorJS 4.2.3", 0, Math.Max(1, totalBytes)));
                using (var reader = archive.ExtractAllEntries())
                {
                    while (reader.MoveToNextEntry())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var key = NormalizeEntryKey(reader.Entry.Key);
                        if (key.Length == 0) continue;
                        if (!targets.TryGetValue(key, out var target))
                            throw new InvalidDataException($"Unexpected EmulatorJS archive entry: {reader.Entry.Key}");
                        if (target.IsDirectory)
                        {
                            Directory.CreateDirectory(target.Path);
                            continue;
                        }

                        var parent = Path.GetDirectoryName(target.Path);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        using (var input = reader.OpenEntryStream())
                        using (var output = new FileStream(target.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[1024 * 1024];
                            long writtenForEntry = 0;
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                output.Write(buffer, 0, read);
                                writtenForEntry += read;
                                reportProgress?.Invoke(new RuntimeInstallProgress(
                                    "Extracting EmulatorJS 4.2.3",
                                    completed + writtenForEntry,
                                    Math.Max(1, totalBytes)));
                            }

                            if (writtenForEntry != target.Size)
                                throw new InvalidDataException($"EmulatorJS entry size mismatch: {key}");
                        }

                        completed += target.Size;
                    }
                }
            }
        }

        private static bool TryFindRuntimeDataDirectory(string root, out string dataPath)
        {
            dataPath = null;
            if (!Directory.Exists(root)) return false;
            var marker = Path.Combine(root, ".playnite-web-emulator-runtime");
            if (Directory.GetFiles(root, "loader.js", SearchOption.AllDirectories)
                .Where(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "data", StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList() is var loaders && loaders.Count == 1)
            {
                if (Directory.GetParent(root)?.Name == "emulatorjs" &&
                    (!File.Exists(marker) || !string.Equals(File.ReadAllText(marker).Trim(), EmulatorJsRuntimeManifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase)))
                    return false;
                dataPath = Path.GetDirectoryName(loaders[0]);
                return File.Exists(Path.Combine(dataPath, "emulator.min.js"));
            }

            return false;
        }

        private static void RequireCore(string dataPath, string coreId)
        {
            var coreDirectory = Path.Combine(dataPath, "cores");
            if (!Directory.Exists(coreDirectory) || Directory.GetFiles(coreDirectory, coreId + "-*.data").Length == 0)
            {
                throw new InvalidDataException($"The official EmulatorJS archive is missing core '{coreId}'.");
            }
        }

        private void DeleteManagedDirectory(string path)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(runtimeRoot));
            var target = Path.GetFullPath(path);
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || string.Equals(target, runtimeRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a path outside the managed EmulatorJS runtime directory.");
            Directory.Delete(target, recursive: true);
        }

        private static string NormalizeEntryKey(string key) =>
            (key ?? string.Empty).Replace('\\', '/').Trim('/');

        private static string EnsureTrailingSeparator(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        private sealed class ExtractionTarget
        {
            public string Path { get; }
            public bool IsDirectory { get; }
            public long Size { get; }

            public ExtractionTarget(string path, bool isDirectory, long size)
            {
                Path = path;
                IsDirectory = isDirectory;
                Size = size;
            }
        }
    }
}

