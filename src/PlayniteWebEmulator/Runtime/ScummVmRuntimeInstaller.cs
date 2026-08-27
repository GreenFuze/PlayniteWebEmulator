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
    internal sealed class ScummVmRuntimeInstaller : IDisposable
    {
        private readonly string runtimeRoot;
        private readonly HttpClient httpClient;
        private readonly SemaphoreSlim installationLock = new SemaphoreSlim(1, 1);

        public ScummVmRuntimeInstaller(string pluginUserDataPath)
        {
            if (string.IsNullOrWhiteSpace(pluginUserDataPath))
                throw new ArgumentException("The plugin user-data path is required.", nameof(pluginUserDataPath));
            runtimeRoot = Path.Combine(
                Path.GetFullPath(pluginUserDataPath),
                "runtimes",
                "scummvm",
                ScummVmRuntimeManifest.DeploymentCommit);
            httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PlayniteWebEmulator/0.1.0");
        }

        public async Task<string> EnsureInstalledAsync(
            string enginePluginFileName,
            Action<RuntimeInstallProgress> reportProgress,
            CancellationToken cancellationToken)
        {
            var requiredFiles = ScummVmRuntimeManifest.GetRequiredFiles(enginePluginFileName);
            await installationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(runtimeRoot);
                var totalBytes = requiredFiles.Sum(file => file.Size);
                long completedBytes = 0;
                foreach (var file in requiredFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var destination = ResolveManagedPath(file.RelativePath);
                    if (!IsValidFile(destination, file))
                    {
                        await DownloadFileAsync(file, destination, completedBytes, totalBytes, reportProgress, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    completedBytes += file.Size;
                    reportProgress?.Invoke(new RuntimeInstallProgress(
                        $"Preparing ScummVM: {file.RelativePath}",
                        completedBytes,
                        totalBytes));
                }

                RequireRuntime(enginePluginFileName);
                return runtimeRoot;
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
            RuntimeFile file,
            string destination,
            long completedBeforeFile,
            long totalBytes,
            Action<RuntimeInstallProgress> reportProgress,
            CancellationToken cancellationToken)
        {
            var parent = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(parent)) throw new InvalidOperationException("A runtime file has no parent directory.");
            Directory.CreateDirectory(parent);
            var temporary = destination + ".download-" + Guid.NewGuid().ToString("N");
            try
            {
                var uri = new Uri(ScummVmRuntimeManifest.BaseUrl + EscapePath(file.RelativePath));
                using (var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != file.Size)
                        throw new InvalidDataException($"ScummVM runtime file size changed for '{file.RelativePath}'.");

                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                    using (var sha256 = SHA256.Create())
                    {
                        var buffer = new byte[1024 * 1024];
                        long written = 0;
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                            sha256.TransformBlock(buffer, 0, read, null, 0);
                            written += read;
                            if (written > file.Size) throw new InvalidDataException($"ScummVM runtime file grew while downloading: {file.RelativePath}");
                            reportProgress?.Invoke(new RuntimeInstallProgress(
                                $"Downloading ScummVM: {file.RelativePath}",
                                completedBeforeFile + written,
                                totalBytes));
                        }
                        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        Validate(file, written, Hex(sha256.Hash));
                    }
                }

                if (File.Exists(destination)) File.Delete(destination);
                File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private bool IsValidFile(string path, RuntimeFile expected)
        {
            if (!File.Exists(path)) return false;
            var info = new FileInfo(path);
            if (info.Length != expected.Size) return false;
            using (var input = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
                return string.Equals(Hex(sha256.ComputeHash(input)), expected.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        private void RequireRuntime(string enginePluginFileName)
        {
            foreach (var relativePath in new[]
            {
                "scummvm.js",
                "scummvm.wasm",
                "data/translations.dat",
                "data/gui-icons.dat",
                "data/scummremastered.zip",
                "data/plugins/" + enginePluginFileName
            })
            {
                var path = ResolveManagedPath(relativePath);
                if (!File.Exists(path)) throw new InvalidDataException($"The ScummVM runtime is missing '{relativePath}'.");
            }
        }

        private string ResolveManagedPath(string relativePath)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(runtimeRoot));
            var candidate = Path.GetFullPath(Path.Combine(runtimeRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"ScummVM runtime path escapes its managed root: {relativePath}");
            return candidate;
        }

        private static void Validate(RuntimeFile expected, long actualSize, string actualHash)
        {
            if (actualSize != expected.Size)
                throw new InvalidDataException($"ScummVM runtime file '{expected.RelativePath}' is incomplete: expected {expected.Size}, got {actualSize}.");
            if (!string.Equals(actualHash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"ScummVM SHA-256 mismatch for '{expected.RelativePath}'.");
        }

        private static string EscapePath(string value) =>
            string.Join("/", value.Split('/').Select(Uri.EscapeDataString));

        private static string Hex(byte[] value) =>
            string.Concat(value.Select(item => item.ToString("x2")));

        private static string EnsureTrailingSeparator(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
}
