using PlayniteWebEmulator.Protocol;
using System;
using System.IO.Pipes;

namespace PlayniteWebEmulator.Launcher
{
    internal sealed class LaunchPipeClient
    {
        private const int ConnectionTimeoutMilliseconds = 10000;

        public LaunchResponse Run(LaunchCommandLine command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            using (var pipe = new NamedPipeClientStream(
                ".",
                PipeProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.None))
            {
                try
                {
                    pipe.Connect(ConnectionTimeoutMilliseconds);
                }
                catch (TimeoutException exception)
                {
                    throw new InvalidOperationException(
                        "Playnite's Web Emulator plugin is not accepting launch requests. Restart Playnite and try again.",
                        exception);
                }

                PipeProtocol.Write(pipe, new LaunchRequest
                {
                    ProfileId = command.ProfileId,
                    RomPath = command.RomPath
                });
                return PipeProtocol.Read<LaunchResponse>(pipe);
            }
        }
    }
}

