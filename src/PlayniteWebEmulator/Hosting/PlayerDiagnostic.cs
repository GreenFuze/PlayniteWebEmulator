using System;

namespace PlayniteWebEmulator.Hosting
{
    internal sealed class PlayerDiagnostic
    {
        public string EventName { get; }
        public string Detail { get; }

        public PlayerDiagnostic(string eventName, string detail)
        {
            EventName = string.IsNullOrWhiteSpace(eventName) ? "browser-event" : eventName.Trim();
            Detail = detail?.Trim() ?? string.Empty;
        }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Detail) ? EventName : $"{EventName}: {Detail}";
    }
}
