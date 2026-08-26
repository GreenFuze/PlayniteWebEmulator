using System;
using System.Collections.Generic;

namespace PlayniteWebEmulator.Launcher
{
    internal sealed class LaunchCommandLine
    {
        public string ProfileId { get; }
        public string RomPath { get; }

        private LaunchCommandLine(string profileId, string romPath)
        {
            ProfileId = profileId;
            RomPath = romPath;
        }

        public static LaunchCommandLine Parse(IReadOnlyList<string> args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            string profileId = null;
            string romPath = null;
            for (var index = 0; index < args.Count; index++)
            {
                var name = args[index];
                if (!string.Equals(name, "--profile", StringComparison.Ordinal) &&
                    !string.Equals(name, "--rom", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Unknown launcher argument '{name}'.", nameof(args));
                }

                if (index + 1 >= args.Count)
                {
                    throw new ArgumentException($"Launcher argument '{name}' requires a value.", nameof(args));
                }

                var value = args[++index];
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Launcher argument '{name}' cannot be empty.", nameof(args));
                }

                if (string.Equals(name, "--profile", StringComparison.Ordinal))
                {
                    if (profileId != null) throw new ArgumentException("The --profile argument was provided more than once.", nameof(args));
                    profileId = value.Trim();
                }
                else
                {
                    if (romPath != null) throw new ArgumentException("The --rom argument was provided more than once.", nameof(args));
                    romPath = value.Trim();
                }
            }

            if (profileId == null) throw new ArgumentException("The --profile argument is required.", nameof(args));
            if (romPath == null) throw new ArgumentException("The --rom argument is required.", nameof(args));
            return new LaunchCommandLine(profileId, romPath);
        }
    }
}

