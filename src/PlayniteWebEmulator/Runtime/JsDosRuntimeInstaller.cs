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
    internal sealed class JsDosRuntimeInstaller : IDisposable
    {
        private const long MaximumExpandedBytes = 32L * 1024 * 1024;
        private const int MaximumEntries = 200;
        private readonly string runtimeRoot;
        private readonly HttpClient httpClient;
        private readonly SemaphoreSlim installationLock = new SemaphoreSlim(1, 1);

        public JsDosRuntimeInstaller(string pluginUserDataPath)
        {
            if (string.IsNullOrWhiteSpace(pluginUserDataPath))
                throw new ArgumentException("The plugin user-data path is required.", nameof(pluginUserDataPath));
            runtimeRoot = Path.Combine(Path.GetFullPath(pluginUserDataPath), "runtimes", "jsdos");
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
                var finalPath = Path.Combine(runtimeRoot, JsDosRuntimeManifest.Version);
                if (IsValidRuntime(finalPath))
                {
                    reportProgress?.Invoke(new RuntimeInstallProgress("js-dos is ready", 1, 1));
                    return finalPath;
                }

                Directory.CreateDirectory(runtimeRoot);
                if (Directory.Exists(finalPath)) DeleteManagedDirectory(finalPath);
                var installRoot = Path.Combine(runtimeRoot, ".install-" + Guid.NewGuid().ToString("N"));
                var contentRoot = Path.Combine(installRoot, "content");
                var archivePath = Path.Combine(installRoot, "js-dos-" + JsDosRuntimeManifest.Version + ".zip");
                Directory.CreateDirectory(contentRoot);
                try
                {
                    await DownloadFileAsync(
                        JsDosRuntimeManifest.ArchiveUrl,
                        archivePath,
                        JsDosRuntimeManifest.ArchiveSize,
                        JsDosRuntimeManifest.ArchiveSha256,
                        "Downloading js-dos " + JsDosRuntimeManifest.Version,
                        reportProgress,
                        cancellationToken).ConfigureAwait(false);
                    ExtractDistribution(archivePath, contentRoot, reportProgress, cancellationToken);
                    await DownloadFileAsync(
                        JsDosRuntimeManifest.LicenseUrl,
                        Path.Combine(contentRoot, "LICENSE-GPL-2.0.txt"),
                        JsDosRuntimeManifest.LicenseSize,
                        JsDosRuntimeManifest.LicenseSha256,
                        "Downloading js-dos license",
                        reportProgress,
                        cancellationToken).ConfigureAwait(false);
                    File.WriteAllText(Path.Combine(contentRoot, "SOURCE.txt"), JsDosRuntimeManifest.SourceNotice);
                    File.WriteAllText(Path.Combine(contentRoot, ".playnite-web-emulator-runtime"), JsDosRuntimeManifest.ArchiveSha256);
                    RequireRuntime(contentRoot);
                    Directory.Move(contentRoot, finalPath);
                    if (!IsValidRuntime(finalPath))
                        throw new InvalidDataException("The installed js-dos runtime could not be validated.");
                    return finalPath;
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

        private async Task DownloadFileAsync(
            string url,
            string destination,
            long expectedSize,
            string expectedSha256,
            string phase,
            Action<RuntimeInstallProgress> reportProgress,
            CancellationToken cancellationToken)
        {
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != expectedSize)
                    throw new InvalidDataException($"js-dos download size changed for '{url}'.");
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                using (var sha256 = SHA256.Create())
                {
                    var buffer = new byte[1024 * 1024];
                    long written = 0;
                    int read;
                    reportProgress?.Invoke(new RuntimeInstallProgress(phase, 0, expectedSize));
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                        sha256.TransformBlock(buffer, 0, read, null, 0);
                        written += read;
                        if (written > expectedSize) throw new InvalidDataException("A js-dos download exceeded its pinned size.");
                        reportProgress?.Invoke(new RuntimeInstallProgress(phase, written, expectedSize));
                    }
                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    ValidateDownload(url, written, expectedSize, Hex(sha256.Hash), expectedSha256);
                }
            }
        }

        private static void ExtractDistribution(
            string archivePath,
            string destinationRoot,
            Action<RuntimeInstallProgress> reportProgress,
            CancellationToken cancellationToken)
        {
            using (var archive = ArchiveFactory.Open(archivePath))
            {
                if (archive.Type != ArchiveType.Zip || !archive.IsComplete || archive.Volumes.Count() != 1)
                    throw new InvalidDataException("The js-dos package is not a complete single-volume ZIP archive.");
                var entries = archive.Entries.ToList();
                if (entries.Count == 0 || entries.Count > MaximumEntries)
                    throw new InvalidDataException("The js-dos archive entry count is invalid.");

                var root = EnsureTrailingSeparator(Path.GetFullPath(destinationRoot));
                var targets = new Dictionary<string, ExtractionTarget>(StringComparer.Ordinal);
                var requiredPaths = new HashSet<string>(
                    JsDosRuntimeManifest.RequiredFiles.Select(file => file.RelativePath),
                    StringComparer.Ordinal);
                long totalBytes = 0;
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.IsEncrypted || entry.IsSplitAfter || !string.IsNullOrWhiteSpace(entry.LinkTarget))
                        throw new InvalidDataException($"Unsupported js-dos archive entry: {entry.Key}");
                    var key = NormalizeEntryKey(entry.Key);
                    if (key.Length == 0 || string.Equals(key, "dist", StringComparison.Ordinal)) continue;
                    if (!key.StartsWith("dist/", StringComparison.Ordinal))
                        throw new InvalidDataException($"Unexpected js-dos archive entry outside dist/: {entry.Key}");
                    var relative = key.Substring("dist/".Length);
                    if (relative.Length == 0) continue;
                    if (entry.IsDirectory || !requiredPaths.Contains(relative)) continue;
                    var destination = Path.GetFullPath(Path.Combine(destinationRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"js-dos archive entry escapes the destination: {entry.Key}");
                    if (targets.ContainsKey(key)) throw new InvalidDataException($"Duplicate js-dos archive entry: {entry.Key}");
                    targets.Add(key, new ExtractionTarget(destination, false, entry.Size));
                    checked { totalBytes += entry.Size; }
                    if (totalBytes > MaximumExpandedBytes)
                        throw new InvalidDataException("The js-dos archive expands beyond the safety limit.");
                }
                if (targets.Count != requiredPaths.Count)
                    throw new InvalidDataException("The js-dos archive is missing one or more pinned runtime files.");

                long completed = 0;
                reportProgress?.Invoke(new RuntimeInstallProgress("Extracting js-dos " + JsDosRuntimeManifest.Version, 0, Math.Max(1, totalBytes)));
                using (var reader = archive.ExtractAllEntries())
                {
                    while (reader.MoveToNextEntry())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var key = NormalizeEntryKey(reader.Entry.Key);
                        if (!targets.TryGetValue(key, out var target)) continue;
                        if (target.IsDirectory)
                        {
                            Directory.CreateDirectory(target.Path);
                            continue;
                        }
                        var parent = Path.GetDirectoryName(target.Path);
                        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                        using (var input = reader.OpenEntryStream())
                        using (var output = new FileStream(target.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[1024 * 1024];
                            long written = 0;
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                output.Write(buffer, 0, read);
                                written += read;
                                reportProgress?.Invoke(new RuntimeInstallProgress(
                                    "Extracting js-dos " + JsDosRuntimeManifest.Version,
                                    completed + written,
                                    Math.Max(1, totalBytes)));
                            }
                            if (written != target.Size) throw new InvalidDataException($"js-dos entry size mismatch: {key}");
                        }
                        completed += target.Size;
                    }
                }
            }
        }

        private static bool IsValidRuntime(string path)
        {
            if (!Directory.Exists(path)) return false;
            var marker = Path.Combine(path, ".playnite-web-emulator-runtime");
            if (!File.Exists(marker) || !string.Equals(File.ReadAllText(marker).Trim(), JsDosRuntimeManifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                return false;
            try
            {
                RequireRuntime(path);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private static void RequireRuntime(string path)
        {
            foreach (var file in JsDosRuntimeManifest.RequiredFiles)
            {
                var candidate = ResolveChild(path, file.RelativePath);
                if (!File.Exists(candidate) || new FileInfo(candidate).Length != file.Size)
                    throw new InvalidDataException($"The js-dos runtime is missing or changed: {file.RelativePath}");
                using (var input = File.OpenRead(candidate))
                using (var sha256 = SHA256.Create())
                {
                    if (!string.Equals(Hex(sha256.ComputeHash(input)), file.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"The js-dos runtime file failed validation: {file.RelativePath}");
                }
            }
            foreach (var required in new[] { "LICENSE-GPL-2.0.txt", "SOURCE.txt" })
            {
                if (!File.Exists(ResolveChild(path, required)))
                    throw new InvalidDataException($"The js-dos runtime is missing its compliance file: {required}");
            }
        }

        private void DeleteManagedDirectory(string path)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(runtimeRoot));
            var target = Path.GetFullPath(path);
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || string.Equals(target, Path.GetFullPath(runtimeRoot), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a path outside the managed js-dos runtime directory.");
            Directory.Delete(target, true);
        }

        private static string ResolveChild(string rootPath, string relativePath)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
            var candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"js-dos runtime path escapes its root: {relativePath}");
            return candidate;
        }

        private static void ValidateDownload(string name, long size, long expectedSize, string hash, string expectedHash)
        {
            if (size != expectedSize) throw new InvalidDataException($"js-dos download '{name}' is incomplete.");
            if (!string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"js-dos SHA-256 mismatch for '{name}'.");
        }

        private static string NormalizeEntryKey(string key) => (key ?? string.Empty).Replace('\\', '/').Trim('/');
        private static string EnsureTrailingSeparator(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        private static string Hex(byte[] value) => string.Concat(value.Select(item => item.ToString("x2")));

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
