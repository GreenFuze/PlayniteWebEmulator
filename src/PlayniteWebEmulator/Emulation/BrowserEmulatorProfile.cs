using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteWebEmulator.Emulation
{
    internal sealed class BrowserEmulatorProfile
    {
        public string Id { get; }
        public string Name { get; }
        public string RuntimeId { get; }
        public string CoreId { get; }
        public string ControlSchemeId { get; }
        public string PlatformSpecificationId { get; }
        public string PlatformName { get; }
        public IReadOnlyList<string> ImageExtensions { get; }
        public bool SupportsRetroAchievements { get; }

        public BrowserEmulatorProfile(
            string id,
            string name,
            string runtimeId,
            string coreId,
            string controlSchemeId,
            string platformSpecificationId,
            string platformName,
            IEnumerable<string> imageExtensions,
            bool supportsRetroAchievements)
        {
            Id = Required(id, nameof(id));
            Name = Required(name, nameof(name));
            RuntimeId = Required(runtimeId, nameof(runtimeId));
            CoreId = string.IsNullOrWhiteSpace(coreId) ? null : coreId.Trim();
            ControlSchemeId = string.IsNullOrWhiteSpace(controlSchemeId) ? null : controlSchemeId.Trim();
            PlatformSpecificationId = Required(platformSpecificationId, nameof(platformSpecificationId));
            PlatformName = Required(platformName, nameof(platformName));
            ImageExtensions = (imageExtensions ?? throw new ArgumentNullException(nameof(imageExtensions)))
                .Select(extension => Required(extension, nameof(imageExtensions)).TrimStart('.').ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ImageExtensions.Count == 0)
            {
                throw new ArgumentException("At least one image extension is required.", nameof(imageExtensions));
            }

            SupportsRetroAchievements = supportsRetroAchievements;
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value.Trim();
        }
    }
}
