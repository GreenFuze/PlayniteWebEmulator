using PlayniteWebEmulator.Protocol;
using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteWebEmulator.Interop
{
    internal sealed class LaunchPipeServer : IDisposable
    {
        private readonly Func<LaunchRequest, LaunchResponse> requestHandler;
        private readonly object synchronization = new object();
        private CancellationTokenSource cancellation;
        private NamedPipeServerStream waitingServer;
        private Task worker;

        public LaunchPipeServer(Func<LaunchRequest, LaunchResponse> requestHandler)
        {
            this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
        }

        public void Start()
        {
            lock (synchronization)
            {
                if (worker != null)
                {
                    throw new InvalidOperationException("The Web Emulator launch server is already running.");
                }

                cancellation = new CancellationTokenSource();
                worker = Task.Run(() => Run(cancellation.Token));
            }
        }

        public void Stop()
        {
            Task workerToWait;
            lock (synchronization)
            {
                if (worker == null) return;
                cancellation.Cancel();
                waitingServer?.Dispose();
                workerToWait = worker;
                worker = null;
            }

            try
            {
                workerToWait.Wait(TimeSpan.FromSeconds(3));
            }
            catch (AggregateException exception) when (exception.GetBaseException() is OperationCanceledException)
            {
            }
            finally
            {
                lock (synchronization)
                {
                    waitingServer = null;
                    cancellation.Dispose();
                    cancellation = null;
                }
            }
        }

        public void Dispose() => Stop();

        private void Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using (var server = new NamedPipeServerStream(
                    PipeProtocol.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous))
                {
                    lock (synchronization)
                    {
                        waitingServer = server;
                    }

                    try
                    {
                        server.WaitForConnection();
                        if (cancellationToken.IsCancellationRequested) return;
                        HandleConnection(server);
                    }
                    catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        if (server.IsConnected)
                        {
                            TryWriteFailure(server, exception.GetBaseException().Message);
                        }
                    }
                    finally
                    {
                        lock (synchronization)
                        {
                            if (ReferenceEquals(waitingServer, server)) waitingServer = null;
                        }
                    }
                }
            }
        }

        private void HandleConnection(NamedPipeServerStream server)
        {
            var request = PipeProtocol.Read<LaunchRequest>(server);
            var response = requestHandler(request)
                ?? throw new InvalidOperationException("The launch handler returned no response.");
            PipeProtocol.Write(server, response);
        }

        private static void TryWriteFailure(NamedPipeServerStream server, string message)
        {
            try
            {
                PipeProtocol.Write(server, LaunchResponse.Failure(message));
            }
            catch
            {
                // The launcher may already have exited. The plugin logs the source exception.
            }
        }
    }
}

