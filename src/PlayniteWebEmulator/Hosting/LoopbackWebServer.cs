using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteWebEmulator.Hosting
{
    internal sealed class LoopbackWebServer : IDisposable
    {
        private readonly string route;
        private readonly byte[] page;
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Task worker;

        public Uri Address { get; }

        public LoopbackWebServer(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new ArgumentException("A player page is required.", nameof(html));
            }

            page = Encoding.UTF8.GetBytes(html);
            route = "/session/" + Guid.NewGuid().ToString("N") + "/";
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            Address = new Uri($"http://127.0.0.1:{endpoint.Port}{route}");
            worker = Task.Run(() => Run(cancellation.Token));
        }

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
                cancellation.Dispose();
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
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true))
            {
                var requestLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(requestLine)) return;
                string header;
                do
                {
                    header = reader.ReadLine();
                }
                while (!string.IsNullOrEmpty(header));

                var parts = requestLine.Split(' ');
                if (parts.Length != 3 || !string.Equals(parts[0], "GET", StringComparison.Ordinal) ||
                    !string.Equals(parts[1], route, StringComparison.Ordinal))
                {
                    WriteResponse(stream, "404 Not Found", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not found."));
                    return;
                }

                WriteResponse(stream, "200 OK", "text/html; charset=utf-8", page);
            }
        }

        private static void WriteResponse(Stream stream, string status, string contentType, byte[] body)
        {
            var headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Cache-Control: no-store\r\n" +
                "X-Content-Type-Options: nosniff\r\n" +
                "Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'\r\n" +
                "Connection: close\r\n\r\n");
            stream.Write(headers, 0, headers.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }
    }
}

