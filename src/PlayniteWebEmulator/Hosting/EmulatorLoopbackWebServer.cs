using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteWebEmulator.Hosting
{
    internal sealed class EmulatorLoopbackWebServer : IDisposable
    {
        private readonly string route;
        private readonly string runtimeRoute;
        private readonly string gameRoute;
        private readonly string gameDirectoryRoute;
        private readonly byte[] page;
        private readonly string runtimeDataRoot;
        private readonly string gamePath;
        private readonly string gameRoot;
        private readonly HashSet<string> gameRelativePaths;
        private readonly Action<PlayerDiagnostic> reportDiagnostic;
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly ManualResetEventSlim pageOpened = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim sessionEnded = new ManualResetEventSlim(false);
        private readonly object activitySynchronization = new object();
        private readonly Task worker;
        private DateTime lastActivityUtc = DateTime.UtcNow;

        public Uri Address { get; }

        public EmulatorLoopbackWebServer(
            string html,
            string runtimeDataRoot,
            string gamePath,
            Action<PlayerDiagnostic> reportDiagnostic = null)
            : this(html, runtimeDataRoot, RequiredFile(gamePath, nameof(gamePath)), null, null, reportDiagnostic)
        {
        }

        private EmulatorLoopbackWebServer(
            string html,
            string runtimeDataRoot,
            string gamePath,
            string gameRoot,
            IEnumerable<string> gameRelativePaths,
            Action<PlayerDiagnostic> reportDiagnostic)
        {
            if (string.IsNullOrWhiteSpace(html)) throw new ArgumentException("A player page is required.", nameof(html));
            this.runtimeDataRoot = RequiredDirectory(runtimeDataRoot, nameof(runtimeDataRoot));
            if ((gamePath == null) == (gameRoot == null))
                throw new ArgumentException("Exactly one game content mode must be configured.");
            this.gamePath = gamePath;
            this.gameRoot = gameRoot == null ? null : RequiredDirectory(gameRoot, nameof(gameRoot));
            this.gameRelativePaths = BuildGameFileSet(this.gameRoot, gameRelativePaths);
            this.reportDiagnostic = reportDiagnostic;
            page = Encoding.UTF8.GetBytes(html);
            route = "/session/" + Guid.NewGuid().ToString("N") + "/";
            runtimeRoute = route + "runtime/";
            gameDirectoryRoute = route + "game/";
            gameRoute = this.gamePath == null ? null : gameDirectoryRoute + Uri.EscapeDataString(Path.GetFileName(this.gamePath));
            listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (System.Net.IPEndPoint)listener.LocalEndpoint;
            Address = new Uri($"http://127.0.0.1:{endpoint.Port}{route}");
            worker = Task.Run(() => Run(cancellation.Token));
        }

        public static EmulatorLoopbackWebServer ForGameDirectory(
            string html,
            string runtimeDataRoot,
            string gameRoot,
            IEnumerable<string> gameRelativePaths,
            Action<PlayerDiagnostic> reportDiagnostic = null) =>
            new EmulatorLoopbackWebServer(
                html,
                runtimeDataRoot,
                null,
                RequiredDirectory(gameRoot, nameof(gameRoot)),
                gameRelativePaths,
                reportDiagnostic);

        public void Dispose()
        {
            cancellation.Cancel();
            listener.Stop();
            try
            {
                worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException exception) when (
                exception.GetBaseException() is OperationCanceledException ||
                exception.GetBaseException() is SocketException ||
                exception.GetBaseException() is ObjectDisposedException)
            {
            }
            finally
            {
                pageOpened.Dispose();
                sessionEnded.Dispose();
                cancellation.Dispose();
            }
        }

        public void WaitForSessionEnd(CancellationToken cancellationToken)
        {
            if (!pageOpened.Wait(TimeSpan.FromSeconds(30), cancellationToken))
                throw new InvalidOperationException("The default browser did not open the Web Emulator player.");

            while (!sessionEnded.Wait(TimeSpan.FromSeconds(1), cancellationToken))
            {
                DateTime lastActivity;
                lock (activitySynchronization) lastActivity = lastActivityUtc;
                if (DateTime.UtcNow - lastActivity > TimeSpan.FromMinutes(3))
                    throw new InvalidOperationException("The browser player stopped responding.");
            }
        }

        private async Task Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _ = Task.Run(() => Handle(client), cancellationToken);
            }
        }

        private void Handle(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 8192, leaveOpen: true))
            {
                var requestLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(requestLine)) return;
                var requestParts = requestLine.Split(' ');
                if (requestParts.Length != 3 ||
                    (!string.Equals(requestParts[0], "GET", StringComparison.Ordinal) &&
                     !string.Equals(requestParts[0], "HEAD", StringComparison.Ordinal) &&
                     !string.Equals(requestParts[0], "POST", StringComparison.Ordinal)))
                {
                    WriteText(stream, "400 Bad Request", "Bad request.");
                    return;
                }

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string header;
                while (!string.IsNullOrEmpty(header = reader.ReadLine()))
                {
                    var separator = header.IndexOf(':');
                    if (separator > 0) headers[header.Substring(0, separator).Trim()] = header.Substring(separator + 1).Trim();
                }

                var requestTarget = requestParts[1];
                var requestPath = requestTarget.Split('?')[0];
                var headOnly = string.Equals(requestParts[0], "HEAD", StringComparison.Ordinal);
                var post = string.Equals(requestParts[0], "POST", StringComparison.Ordinal);
                MarkActivity();
                if (post && !string.Equals(requestPath, route + "diagnostics", StringComparison.Ordinal))
                {
                    WriteText(stream, "405 Method Not Allowed", "Method not allowed.");
                    return;
                }

                if (string.Equals(requestPath, route, StringComparison.Ordinal))
                {
                    pageOpened.Set();
                    WriteBytes(stream, "200 OK", "text/html; charset=utf-8", page, headOnly, true);
                    return;
                }

                if (gamePath != null && string.Equals(requestPath, gameRoute, StringComparison.Ordinal))
                {
                    WriteFile(stream, gamePath, headers, headOnly);
                    return;
                }

                if (gameRoot != null && requestPath.StartsWith(gameDirectoryRoute, StringComparison.Ordinal))
                {
                    var relative = Uri.UnescapeDataString(requestPath.Substring(gameDirectoryRoute.Length));
                    if (!TryResolveGameFile(relative, out var gameFile))
                    {
                        reportDiagnostic?.Invoke(new PlayerDiagnostic("missing-game-resource", relative));
                        WriteText(stream, "404 Not Found", "Not found.");
                        return;
                    }

                    WriteFile(stream, gameFile, headers, headOnly);
                    return;
                }

                if (string.Equals(requestPath, route + "diagnostics", StringComparison.Ordinal))
                {
                    var diagnostic = ReadDiagnostic(requestTarget);
                    if (string.Equals(diagnostic.EventName, "closed", StringComparison.OrdinalIgnoreCase))
                        sessionEnded.Set();
                    reportDiagnostic?.Invoke(diagnostic);
                    WriteBytes(stream, "204 No Content", "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly, false);
                    return;
                }

                if (requestPath.StartsWith(runtimeRoute, StringComparison.Ordinal))
                {
                    var relative = Uri.UnescapeDataString(requestPath.Substring(runtimeRoute.Length));
                    if (!TryResolveRuntimeFile(relative, out var runtimeFile))
                    {
                        reportDiagnostic?.Invoke(new PlayerDiagnostic("missing-runtime-resource", relative));
                        WriteText(stream, "404 Not Found", "Not found.");
                        return;
                    }

                    WriteFile(stream, runtimeFile, headers, headOnly);
                    return;
                }

                reportDiagnostic?.Invoke(new PlayerDiagnostic("unknown-player-request", requestPath));
                WriteText(stream, "404 Not Found", "Not found.");
            }
        }

        private void MarkActivity()
        {
            lock (activitySynchronization) lastActivityUtc = DateTime.UtcNow;
        }

        private static PlayerDiagnostic ReadDiagnostic(string requestTarget)
        {
            var queryStart = requestTarget.IndexOf('?');
            if (queryStart < 0 || queryStart == requestTarget.Length - 1)
                return new PlayerDiagnostic("browser-event", string.Empty);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in requestTarget.Substring(queryStart + 1).Split('&'))
            {
                var separator = pair.IndexOf('=');
                var name = separator < 0 ? pair : pair.Substring(0, separator);
                var value = separator < 0 ? string.Empty : pair.Substring(separator + 1);
                values[Uri.UnescapeDataString(name.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
            }

            values.TryGetValue("event", out var eventName);
            values.TryGetValue("detail", out var detail);
            return new PlayerDiagnostic(eventName, detail);
        }

        private bool TryResolveRuntimeFile(string relativePath, out string filePath)
        {
            filePath = null;
            if (string.IsNullOrWhiteSpace(relativePath)) return false;
            var root = EnsureTrailingSeparator(runtimeDataRoot);
            var candidate = Path.GetFullPath(Path.Combine(runtimeDataRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate)) return false;
            filePath = candidate;
            return true;
        }

        private bool TryResolveGameFile(string relativePath, out string filePath)
        {
            filePath = null;
            var normalized = NormalizeRelativePath(relativePath);
            if (normalized == null || !gameRelativePaths.Contains(normalized)) return false;
            var root = EnsureTrailingSeparator(gameRoot);
            var candidate = Path.GetFullPath(Path.Combine(gameRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate)) return false;
            filePath = candidate;
            return true;
        }

        private static HashSet<string> BuildGameFileSet(string gameRoot, IEnumerable<string> relativePaths)
        {
            if (gameRoot == null) return null;
            if (relativePaths == null) throw new ArgumentNullException(nameof(relativePaths));
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relativePath in relativePaths)
            {
                var normalized = NormalizeRelativePath(relativePath);
                if (normalized == null) throw new InvalidDataException($"Unsafe game content path: {relativePath}");
                if (!result.Add(normalized)) throw new InvalidDataException($"Duplicate game content path: {relativePath}");
            }
            if (result.Count == 0) throw new InvalidDataException("A game directory must expose at least one file.");
            return result;
        }

        private static string NormalizeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Replace('\\', '/').Trim('/');
            var parts = normalized.Split('/');
            if (parts.Length == 0 || parts.Any(part => part.Length == 0 || part == "." || part == "..")) return null;
            return string.Join("/", parts);
        }

        private static void WriteFile(Stream output, string path, IReadOnlyDictionary<string, string> requestHeaders, bool headOnly)
        {
            var info = new FileInfo(path);
            long start = 0;
            long end = info.Length - 1;
            var status = "200 OK";
            if (requestHeaders.TryGetValue("Range", out var rangeHeader) &&
                TryParseRange(rangeHeader, info.Length, out var requestedStart, out var requestedEnd))
            {
                start = requestedStart;
                end = requestedEnd;
                status = "206 Partial Content";
            }

            var length = end - start + 1;
            var headers = new StringBuilder()
                .Append("HTTP/1.1 ").Append(status).Append("\r\n")
                .Append("Content-Type: ").Append(GetContentType(path)).Append("\r\n")
                .Append("Content-Length: ").Append(length).Append("\r\n")
                .Append("Accept-Ranges: bytes\r\n")
                .Append("Cache-Control: public, max-age=31536000, immutable\r\n")
                .Append("X-Content-Type-Options: nosniff\r\n");
            if (status.StartsWith("206", StringComparison.Ordinal))
                headers.Append("Content-Range: bytes ").Append(start).Append('-').Append(end).Append('/').Append(info.Length).Append("\r\n");
            headers.Append("Connection: close\r\n\r\n");
            var headerBytes = Encoding.ASCII.GetBytes(headers.ToString());
            output.Write(headerBytes, 0, headerBytes.Length);
            if (headOnly) return;

            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                input.Position = start;
                var buffer = new byte[1024 * 1024];
                var remaining = length;
                while (remaining > 0)
                {
                    var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (read == 0) throw new EndOfStreamException("A served file ended unexpectedly.");
                    output.Write(buffer, 0, read);
                    remaining -= read;
                }
            }
        }

        private static bool TryParseRange(string header, long fileLength, out long start, out long end)
        {
            start = 0;
            end = fileLength - 1;
            if (fileLength <= 0 || string.IsNullOrWhiteSpace(header) || !header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                return false;
            var value = header.Substring("bytes=".Length);
            if (value.Contains(",")) return false;
            var parts = value.Split('-');
            if (parts.Length != 2 || !long.TryParse(parts[0], out start)) return false;
            if (string.IsNullOrWhiteSpace(parts[1])) end = fileLength - 1;
            else if (!long.TryParse(parts[1], out end)) return false;
            return start >= 0 && end >= start && end < fileLength;
        }

        private static void WriteBytes(Stream stream, string status, string contentType, byte[] body, bool headOnly, bool playerPage)
        {
            var headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Cache-Control: no-store\r\n" +
                "X-Content-Type-Options: nosniff\r\n" +
                (playerPage ? "Content-Security-Policy: default-src 'self' blob: data:; script-src 'self' 'unsafe-inline' 'unsafe-eval' blob:; style-src 'self' 'unsafe-inline'; connect-src 'self' blob: data:; worker-src 'self' blob:; img-src 'self' blob: data:; media-src 'self' blob: data:\r\nCross-Origin-Opener-Policy: same-origin\r\nCross-Origin-Embedder-Policy: require-corp\r\nCross-Origin-Resource-Policy: same-origin\r\nPermissions-Policy: fullscreen=(self)\r\n" : string.Empty) +
                "Connection: close\r\n\r\n");
            stream.Write(headers, 0, headers.Length);
            if (!headOnly) stream.Write(body, 0, body.Length);
        }

        private static void WriteText(Stream stream, string status, string message) =>
            WriteBytes(stream, status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(message), false, false);

        private static string GetContentType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".js": return "application/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".wasm": return "application/wasm";
                case ".so": return "application/wasm";
                case ".svg": return "image/svg+xml";
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".woff2": return "font/woff2";
                default: return "application/octet-stream";
            }
        }

        private static string RequiredDirectory(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A directory is required.", parameterName);
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException(fullPath);
            return fullPath;
        }

        private static string RequiredFile(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file is required.", parameterName);
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("The served game file does not exist.", fullPath);
            return fullPath;
        }

        private static string EnsureTrailingSeparator(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
}
