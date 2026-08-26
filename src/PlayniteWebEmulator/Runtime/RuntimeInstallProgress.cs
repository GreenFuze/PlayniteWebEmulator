using System;

namespace PlayniteWebEmulator.Runtime
{
    internal sealed class RuntimeInstallProgress
    {
        public string Phase { get; }
        public long CompletedBytes { get; }
        public long TotalBytes { get; }

        public RuntimeInstallProgress(string phase, long completedBytes, long totalBytes)
        {
            if (string.IsNullOrWhiteSpace(phase)) throw new ArgumentException("A phase is required.", nameof(phase));
            if (completedBytes < 0) throw new ArgumentOutOfRangeException(nameof(completedBytes));
            if (totalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(totalBytes));
            if (completedBytes > totalBytes) completedBytes = totalBytes;
            Phase = phase.Trim();
            CompletedBytes = completedBytes;
            TotalBytes = totalBytes;
        }
    }
}

