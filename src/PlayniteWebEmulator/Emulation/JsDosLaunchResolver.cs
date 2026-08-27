using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlayniteWebEmulator.Emulation
{
    internal sealed class JsDosLaunchResolver
    {
        private static readonly HashSet<string> RunnableExtensions =
            new HashSet<string>(new[] { ".bat", ".com", ".exe" }, StringComparer.OrdinalIgnoreCase);
        private static readonly string[] ExcludedNameParts =
        {
            "setup", "install", "unins", "uninstall", "config", "readme", "manual", "sound", "driver"
        };

        public JsDosLaunchPlan Resolve(string markerPath, Func<JsDosLaunchSelectionRequest, string> selectCandidate = null)
        {
            var marker = ValidateMarker(markerPath);
            var gameRoot = Path.GetDirectoryName(marker);
            if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
                throw new DirectoryNotFoundException("The js-dos game directory does not exist.");
            var files = Directory.GetFiles(gameRoot, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, marker, StringComparison.OrdinalIgnoreCase))
                .Where(path => !string.Equals(Path.GetFileName(path), ".cloud-storage-install.json", StringComparison.OrdinalIgnoreCase))
                .Select(path => new JsDosGameFile(gameRoot, path))
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0) throw new InvalidDataException("The js-dos game directory contains no game files.");

            var configured = ResolveFromDosBoxConfiguration(gameRoot, files);
            if (configured != null) return new JsDosLaunchPlan(gameRoot, configured.RelativePath, files);

            var candidates = files
                .Where(file => RunnableExtensions.Contains(Path.GetExtension(file.RelativePath)))
                .Select(file => new JsDosLaunchCandidate(file.RelativePath, Score(file.RelativePath, Path.GetFileName(gameRoot))))
                .Where(candidate => candidate.Score >= 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.RelativePath.Count(character => character == '/'))
                .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (candidates.Count == 0)
                throw new InvalidDataException("The DOS game contains no runnable .BAT, .COM, or .EXE file.");
            if (candidates.Count == 1 || (candidates[0].Score >= 50 && candidates[0].Score > candidates[1].Score))
                return new JsDosLaunchPlan(gameRoot, candidates[0].RelativePath, files);
            if (selectCandidate == null)
                throw new InvalidDataException("The DOS game has multiple plausible launchers and requires user selection.");

            var request = new JsDosLaunchSelectionRequest(Path.GetFileName(gameRoot), candidates);
            var selected = selectCandidate(request);
            if (string.IsNullOrWhiteSpace(selected)) throw new OperationCanceledException("DOS launcher selection was canceled.");
            var match = candidates.FirstOrDefault(candidate => string.Equals(candidate.RelativePath, selected, StringComparison.OrdinalIgnoreCase));
            if (match == null) throw new InvalidDataException("The selected DOS launcher is not one of the offered candidates.");
            return new JsDosLaunchPlan(gameRoot, match.RelativePath, files);
        }

        private static JsDosGameFile ResolveFromDosBoxConfiguration(string gameRoot, IReadOnlyList<JsDosGameFile> files)
        {
            var configurations = files
                .Where(file => string.Equals(Path.GetExtension(file.RelativePath), ".conf", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => string.Equals(
                    Path.GetFileNameWithoutExtension(file.RelativePath),
                    Path.GetFileName(gameRoot),
                    StringComparison.OrdinalIgnoreCase))
                .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var configuration in configurations)
            {
                var fullPath = ResolveChild(gameRoot, configuration.RelativePath);
                var launchPath = ParseAutoexecLaunch(File.ReadAllLines(fullPath), files);
                if (launchPath != null)
                    return files.First(file => string.Equals(file.RelativePath, launchPath, StringComparison.OrdinalIgnoreCase));
            }
            return null;
        }

        internal static string ParseAutoexecLaunch(IEnumerable<string> lines, IReadOnlyList<JsDosGameFile> files)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (files == null) throw new ArgumentNullException(nameof(files));
            var inAutoexec = false;
            var workingDirectory = string.Empty;
            string selected = null;
            foreach (var source in lines)
            {
                var line = (source ?? string.Empty).Trim();
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    inAutoexec = string.Equals(line, "[autoexec]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inAutoexec || line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                    continue;
                if (line.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("chdir ", StringComparison.OrdinalIgnoreCase))
                {
                    var argument = line.Substring(line.IndexOf(' ') + 1).Trim().Trim('"').Replace('\\', '/').Trim('/');
                    if (!argument.Contains(":") && !argument.Split('/').Any(part => part == "..")) workingDirectory = argument;
                    continue;
                }
                if (line.StartsWith("call ", StringComparison.OrdinalIgnoreCase)) line = line.Substring(5).Trim();
                if (line.StartsWith("mount ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("imgmount ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("echo", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("set ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("cls", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("goto ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("if ", StringComparison.OrdinalIgnoreCase) ||
                    line.EndsWith(":", StringComparison.Ordinal))
                    continue;
                var token = FirstToken(line).Replace('\\', '/').Trim('/');
                if (token.Length == 0 || token.Contains(":")) continue;
                var relative = string.IsNullOrWhiteSpace(workingDirectory) ? token : workingDirectory + "/" + token;
                var matches = ResolveRunnableMatches(relative, files);
                if (matches.Count == 1) selected = matches[0].RelativePath;
            }
            return selected;
        }

        private static List<JsDosGameFile> ResolveRunnableMatches(string relative, IReadOnlyList<JsDosGameFile> files)
        {
            if (RunnableExtensions.Contains(Path.GetExtension(relative)))
                return files.Where(file => string.Equals(file.RelativePath, relative, StringComparison.OrdinalIgnoreCase)).ToList();
            return files.Where(file => RunnableExtensions.Contains(Path.GetExtension(file.RelativePath)))
                .Where(file => string.Equals(
                    file.RelativePath.Substring(0, file.RelativePath.Length - Path.GetExtension(file.RelativePath).Length),
                    relative,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static string FirstToken(string line)
        {
            if (line.StartsWith("\"", StringComparison.Ordinal))
            {
                var closing = line.IndexOf('"', 1);
                return closing > 1 ? line.Substring(1, closing - 1) : string.Empty;
            }
            var separator = line.IndexOfAny(new[] { ' ', '\t' });
            return separator < 0 ? line : line.Substring(0, separator);
        }

        private static int Score(string relativePath, string gameName)
        {
            var name = Path.GetFileNameWithoutExtension(relativePath);
            if (ExcludedNameParts.Any(part => name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)) return -1;
            var score = 0;
            if (new[] { "go", "start", "run", "play" }.Contains(name, StringComparer.OrdinalIgnoreCase)) score += 70;
            if (string.Equals(Normalize(name), Normalize(gameName), StringComparison.OrdinalIgnoreCase)) score += 60;
            if (string.Equals(Path.GetExtension(relativePath), ".bat", StringComparison.OrdinalIgnoreCase)) score += 10;
            if (!relativePath.Contains("/")) score += 5;
            return score;
        }

        private static string Normalize(string value) => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());

        private static string ValidateMarker(string markerPath)
        {
            if (string.IsNullOrWhiteSpace(markerPath)) throw new ArgumentException("A js-dos marker path is required.", nameof(markerPath));
            var marker = Path.GetFullPath(markerPath);
            if (!File.Exists(marker)) throw new FileNotFoundException("The js-dos marker does not exist.", marker);
            if (!string.Equals(Path.GetExtension(marker), ".jsdos", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A js-dos launch must use a .jsdos marker.");
            return marker;
        }

        private static string ResolveChild(string rootPath, string relativePath)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
            var child = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("A DOS game file escapes its game root.");
            return child;
        }

        private static string EnsureTrailingSeparator(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    internal sealed class JsDosLaunchPlan
    {
        public string GameRoot { get; }
        public string LaunchRelativePath { get; }
        public IReadOnlyList<JsDosGameFile> Files { get; }

        public JsDosLaunchPlan(string gameRoot, string launchRelativePath, IReadOnlyList<JsDosGameFile> files)
        {
            GameRoot = gameRoot ?? throw new ArgumentNullException(nameof(gameRoot));
            LaunchRelativePath = launchRelativePath ?? throw new ArgumentNullException(nameof(launchRelativePath));
            Files = files ?? throw new ArgumentNullException(nameof(files));
            if (Files.Count == 0) throw new ArgumentException("At least one DOS game file is required.", nameof(files));
            if (!Files.Any(file => string.Equals(file.RelativePath, LaunchRelativePath, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("The DOS launcher must be included in the game files.", nameof(launchRelativePath));
        }
    }

    internal sealed class JsDosGameFile
    {
        public string RelativePath { get; }
        public long Size { get; }

        public JsDosGameFile(string gameRoot, string fullPath)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(gameRoot ?? throw new ArgumentNullException(nameof(gameRoot))));
            var file = Path.GetFullPath(fullPath ?? throw new ArgumentNullException(nameof(fullPath)));
            if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("A DOS game file escapes its game root.");
            if (!File.Exists(file)) throw new FileNotFoundException("A DOS game file is missing.", file);
            RelativePath = file.Substring(root.Length).Replace('\\', '/');
            if (RelativePath.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidDataException("A DOS game file has an unsafe relative path.");
            Size = new FileInfo(file).Length;
        }

        private static string EnsureTrailingSeparator(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    internal sealed class JsDosLaunchCandidate
    {
        public string RelativePath { get; }
        public int Score { get; }

        public JsDosLaunchCandidate(string relativePath, int score)
        {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            Score = score;
        }
    }

    internal sealed class JsDosLaunchSelectionRequest
    {
        public string GameName { get; }
        public IReadOnlyList<JsDosLaunchCandidate> Candidates { get; }

        public JsDosLaunchSelectionRequest(string gameName, IReadOnlyList<JsDosLaunchCandidate> candidates)
        {
            GameName = gameName ?? throw new ArgumentNullException(nameof(gameName));
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            if (Candidates.Count < 2) throw new ArgumentException("Ambiguous DOS selection requires at least two candidates.", nameof(candidates));
        }
    }
}
