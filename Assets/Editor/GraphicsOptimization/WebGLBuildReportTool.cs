using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GraphicsOptimization
{
    public static class WebGLBuildReportTool
    {
        private static readonly Regex CandidatePattern = new Regex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$");
        private const string ReportToolVersion = "2";
        private const int ReportSchemaVersion = 2;
        private static readonly string[] RequiredPlayerFiles =
        {
            "index.html",
            "Build/Player.data.unityweb",
            "Build/Player.framework.js.unityweb",
            "Build/Player.loader.js",
            "Build/Player.wasm.unityweb"
        };

        public static bool IsValidCandidate(string candidate)
        {
            return candidate != null && CandidatePattern.IsMatch(candidate);
        }

        public static string NormalizeCandidateIdentity(string candidate)
        {
            if (!IsValidCandidate(candidate))
            {
                throw new ArgumentException("Candidate must be a safe build-directory name.", nameof(candidate));
            }

            return candidate.ToLowerInvariant();
        }

        public static BuildOptions GetReleaseBuildOptions()
        {
            return BuildOptions.DetailedBuildReport;
        }

        public static string[] GetEnabledScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        public static string GetCandidateOutputDirectory(string candidate)
        {
            candidate = NormalizeCandidateIdentity(candidate);
            var root = GetBuildRoot();
            var output = Path.GetFullPath(Path.Combine(root, candidate));
            if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Build output escapes the GraphicsOptimization root.");
            }

            return output;
        }

        public static bool IsReparsePoint(FileAttributes attributes)
        {
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }

        public static IDisposable TryAcquireCandidateLease(string trustedRoot, string directory, string candidate)
        {
            NormalizeCandidateIdentity(candidate);
            EnsureNoReparsePoints(trustedRoot, directory);
            var lockPath = Path.Combine(directory, ".webgl-build.lock");
            EnsureNoReparsePoints(trustedRoot, lockPath);
            try
            {
                return new CandidateLease(trustedRoot, directory, lockPath,
                    new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException exception) when (IsAlreadyExists(exception))
            {
                return null;
            }
        }

        private static bool IsAlreadyExists(IOException exception)
        {
            var error = exception.HResult & 0xFFFF;
            return error == 80 || error == 183;
        }

        private static string ResolveCandidate()
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == "-graphicsCandidate") return args[index + 1];
            }

            return Environment.GetEnvironmentVariable("GRAPHICS_OPT_CANDIDATE")
                ?? "build-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        }

        [MenuItem("Tools/Graphics Optimization/Build Release WebGL Report")]
        public static void BuildReleaseWebGLReport()
        {
            var requestedCandidate = ResolveCandidate();
            var requestedCandidateIsValid = IsValidCandidate(requestedCandidate);
            var candidate = requestedCandidateIsValid
                ? NormalizeCandidateIdentity(requestedCandidate)
                : "invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            EnsureTrustedBuildRoot();
            var directory = GetCandidateOutputDirectory(candidate);
            EnsureNoReparsePoints(projectRoot, directory);
            Directory.CreateDirectory(directory);
            EnsureNoReparsePoints(projectRoot, directory);
            using (var lease = TryAcquireCandidateLease(projectRoot, directory, candidate))
            {
                if (lease == null)
                {
                    throw new InvalidOperationException("A Release WebGL build is already running for candidate '" + candidate + "'. Stale locks are never stolen automatically.");
                }

                if (HasSuccessfulBuildReport(directory, candidate))
                {
                    Debug.LogWarning("Release WebGL candidate '" + candidate + "' already succeeded; preserving its reports and Player output.");
                    return;
                }

                var scenes = GetEnabledScenePaths();
                var options = GetReleaseBuildOptions();
                BuildReport report = null;
                Exception failure = null;
                try
                {
                    if (!requestedCandidateIsValid) throw new ArgumentException("Invalid graphics candidate.");
                    var player = GetPlayerOutputDirectory(directory);
                    if (Directory.Exists(player)) DeletePlayerDirectorySafely(projectRoot, directory, player);
                    WriteInputs(directory, candidate, scenes, options);
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = player,
                        target = BuildTarget.WebGL,
                        targetGroup = BuildTargetGroup.WebGL,
                        options = options
                    });
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    WriteReports(directory, candidate, scenes, options, report, failure);
                }

                if (failure != null) throw failure;
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Release WebGL build did not succeed; inspect build-report.json.");
            }
        }

        private static string GetPlayerOutputDirectory(string directory)
        {
            var player = Path.GetFullPath(Path.Combine(directory, "Player"));
            if (!player.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Player output escapes its candidate directory.");
            }

            return player;
        }

        private static string GetBuildRoot()
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Builds", "GraphicsOptimization"));
        }

        private static string EnsureTrustedBuildRoot()
        {
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            var root = GetBuildRoot();
            EnsureNoReparsePoints(projectRoot, root);
            Directory.CreateDirectory(root);
            EnsureNoReparsePoints(projectRoot, root);
            return root;
        }

        private static void DeletePlayerDirectorySafely(string root, string directory, string player)
        {
            EnsureNoReparsePoints(root, directory);
            DeleteDirectoryTreeSafely(root, player);
        }

        private static void DeleteDirectoryTreeSafely(string trustedRoot, string directory)
        {
            EnsureNoReparsePoints(trustedRoot, directory);
            if (!Directory.Exists(directory)) return;

            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if (IsReparsePoint(attributes)) throw new InvalidOperationException("Build tree contains a reparse point.");
                if ((attributes & FileAttributes.Directory) != 0) DeleteDirectoryTreeSafely(trustedRoot, entry);
                else DeleteFileEntrySafely(trustedRoot, entry);
            }

            EnsureNoReparsePoints(trustedRoot, directory);
            Directory.Delete(directory, false);
        }

        private static void EnsureNoReparsePoints(string trustedRoot, string target)
        {
            if (HasReparsePointInExistingPath(trustedRoot, target))
            {
                throw new InvalidOperationException("Build path contains a reparse point.");
            }
        }

        private static void EnsureNoReparsePointsInTree(string trustedRoot, string directory)
        {
            EnsureNoReparsePoints(trustedRoot, directory);
            if (!Directory.Exists(directory)) return;

            var pending = new Stack<string>();
            pending.Push(directory);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    if (IsReparsePoint(File.GetAttributes(entry)))
                    {
                        throw new InvalidOperationException("Build tree contains a reparse point.");
                    }

                    if (Directory.Exists(entry)) pending.Push(entry);
                }
            }
        }

        private static bool HasReparsePointInExistingPath(string trustedRoot, string target)
        {
            try
            {
                trustedRoot = Path.GetFullPath(trustedRoot);
                target = Path.GetFullPath(target);
                var trustedPrefix = trustedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    || trustedRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? trustedRoot
                    : trustedRoot + Path.DirectorySeparatorChar;
                if (!string.Equals(target, trustedRoot, StringComparison.OrdinalIgnoreCase)
                    && !target.StartsWith(trustedPrefix, StringComparison.OrdinalIgnoreCase)) return true;

                var relative = target.Substring(trustedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var current = trustedRoot;
                if (Directory.Exists(current) && IsReparsePoint(File.GetAttributes(current))) return true;
                foreach (var component in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
                {
                    current = Path.Combine(current, component);
                    if ((Directory.Exists(current) || File.Exists(current)) && IsReparsePoint(File.GetAttributes(current))) return true;
                }

                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static bool HasSuccessfulBuildReport(string directory, string candidate)
        {
            return IsValidCompletedCandidate(directory, candidate);
        }

        public static bool IsValidCompletedCandidate(string directory, string candidate)
        {
            if (!IsValidCandidate(candidate) || string.IsNullOrEmpty(directory)) return false;

            try
            {
                candidate = NormalizeCandidateIdentity(candidate);
                directory = Path.GetFullPath(directory);
                var trustedRoot = Path.GetPathRoot(directory);
                EnsureNoReparsePoints(trustedRoot, directory);
                var reportPath = Path.Combine(directory, "build-report.json");
                var inputPath = Path.Combine(directory, "build-inputs.json");
                var manifestPath = Path.Combine(directory, "artifact-manifest.json");
                if (!IsSafeExistingFile(trustedRoot, reportPath) || !IsSafeExistingFile(trustedRoot, inputPath) ||
                    !IsSafeExistingFile(trustedRoot, manifestPath)) return false;

                var expectedInputs = BuildInputsJson(candidate, GetEnabledScenePaths(), GetReleaseBuildOptions());
                var inputs = File.ReadAllText(inputPath);
                if (!string.Equals(inputs, expectedInputs, StringComparison.Ordinal)) return false;
                var inputHash = Sha256(inputs);
                var manifestContents = File.ReadAllText(manifestPath);
                if (!TryReadSuccessfulReport(File.ReadAllText(reportPath), candidate, inputHash, out var totalSize, out var manifestHash)) return false;
                if (!string.Equals(manifestHash, Sha256(manifestContents), StringComparison.Ordinal)) return false;
                var manifest = JsonUtility.FromJson<ArtifactManifestRecord>(manifestContents);
                if (manifest == null || manifest.schema_version != ReportSchemaVersion ||
                    !string.Equals(manifest.input_sha256, inputHash, StringComparison.Ordinal)) return false;

                var player = GetPlayerOutputDirectory(directory);
                EnsureNoReparsePointsInTree(trustedRoot, player);
                if (!MatchesRequiredOutputs(player, manifest.outputs)) return false;
                var playerFiles = Directory.GetFiles(player, "*", SearchOption.AllDirectories);
                var playerSize = playerFiles.Sum(file => new FileInfo(file).Length);
                var newestPlayerWrite = playerFiles.Max(File.GetLastWriteTimeUtc);
                return playerSize == totalSize && File.GetLastWriteTimeUtc(reportPath) >= newestPlayerWrite;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReadSuccessfulReport(string contents, string candidate, string inputHash, out long totalSize, out string manifestHash)
        {
            totalSize = 0;
            manifestHash = "";
            try
            {
                var report = JsonUtility.FromJson<BuildReportRecord>(contents);
                if (report == null || report.schema_version != ReportSchemaVersion || report.tool_version != ReportToolVersion ||
                    !string.Equals(report.candidate, candidate, StringComparison.Ordinal) || report.result != "Succeeded" ||
                    !report.release || report.development || report.profiler || report.debugging || report.errors != 0 ||
                    !string.IsNullOrEmpty(report.failure) || !string.IsNullOrEmpty(report.failure_code) || report.total_size <= 0 ||
                    !string.Equals(report.input_sha256, inputHash, StringComparison.Ordinal) || !IsSha256(report.manifest_sha256))
                {
                    return false;
                }

                totalSize = report.total_size;
                manifestHash = report.manifest_sha256;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool MatchesRequiredOutputs(string player, OutputHashRecord[] outputs)
        {
            if (outputs == null || outputs.Length != RequiredPlayerFiles.Length) return false;
            var byPath = outputs.ToDictionary(output => output.relative_path, StringComparer.Ordinal);
            if (byPath.Count != RequiredPlayerFiles.Length) return false;
            foreach (var required in RequiredPlayerFiles)
            {
                if (!byPath.TryGetValue(required, out var output)) return false;
                var file = Path.Combine(player, required.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file) || output.size_bytes <= 0 || new FileInfo(file).Length != output.size_bytes ||
                    !string.Equals(Sha256File(file), output.sha256, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        private static void WriteInputs(string directory, string candidate, string[] scenes, BuildOptions options)
        {
            WriteAllText(directory, "build-inputs.json", BuildInputsJson(candidate, scenes, options));
        }

        private static void WriteReports(string directory, string candidate, string[] scenes, BuildOptions options, BuildReport report, Exception failure)
        {
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            EnsureNoReparsePoints(projectRoot, directory);
            EnsureNoReparsePointsInTree(projectRoot, GetPlayerOutputDirectory(directory));
            var result = report == null ? "Failed" : report.summary.result.ToString();
            var errors = report == null ? 1 : report.summary.totalErrors;
            var warnings = report == null ? 0 : report.summary.totalWarnings;
            var totalSize = report == null ? 0 : report.summary.totalSize;
            var inputHash = Sha256(BuildInputsJson(candidate, scenes, options));
            var manifestHash = "";
            if (failure == null && report != null && report.summary.result == BuildResult.Succeeded)
            {
                var manifest = ArtifactManifestJson(inputHash, GetPlayerOutputDirectory(directory));
                manifestHash = Sha256(manifest);
                WriteAllText(directory, "artifact-manifest.json", manifest);
            }

            WriteAllText(directory, "build-report.json", "{\"schema_version\":" + ReportSchemaVersion + ",\"tool_version\":\"" + ReportToolVersion + "\",\"candidate\":\"" + Json(candidate) + "\",\"result\":\"" + Json(result) + "\",\"release\":true,\"development\":false,\"profiler\":false,\"debugging\":false,\"errors\":" + errors + ",\"warnings\":" + warnings + ",\"total_size\":" + totalSize + ",\"input_sha256\":\"" + inputHash + "\",\"manifest_sha256\":\"" + manifestHash + "\",\"failure_code\":\"" + FailureCode(failure) + "\",\"failure\":\"" + Json(SanitizeFailure(failure)) + "\"}");
            WriteAllText(directory, "build-summary.csv", "field,value\nresult," + Csv(result) + "\nrelease,true\ndevelopment,false\nprofiler,false\ndebugging,false\nerrors," + errors + "\nwarnings," + warnings + "\ntotal_size," + totalSize + "\n");
            WriteAllText(directory, "build-steps.csv", FormatBuildStepsForReport(report == null ? null : report.steps));
            WriteAllText(directory, "packed-assets.csv", FormatPackedAssetsForReport(PackedAssetRows(report)));
            WriteAllText(directory, "scene-dependencies.csv", SceneDependencies(scenes));
            WriteAllText(directory, "importer-inventory.csv", Importers(scenes));
            WriteAllText(directory, "output-files.csv", OutputFiles(Path.Combine(directory, "Player")));
        }

        private static string BuildInputsJson(string candidate, string[] scenes, BuildOptions options)
        {
            return "{\"schema_version\":" + ReportSchemaVersion + ",\"tool_version\":\"" + ReportToolVersion + "\",\"candidate\":\"" + Json(candidate) + "\",\"release\":true,\"development\":false,\"profiler\":false,\"debugging\":false,\"unity_version\":\"" + Json(Application.unityVersion) + "\",\"options\":\"" + Json(options.ToString()) + "\",\"source_fingerprint_reference\":\"Builds/GraphicsOptimization/baseline/source-fingerprint/git-state.json\",\"source_fingerprint_sha256\":\"" + SourceFingerprintHash() + "\",\"scenes\":[" + JsonArray(scenes) + "]}";
        }

        private static string SourceFingerprintHash()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "GraphicsOptimization", "baseline", "source-fingerprint", "git-state.json");
            return File.Exists(path) ? Sha256File(path) : "";
        }

        private static string ArtifactManifestJson(string inputHash, string player)
        {
            var outputs = RequiredPlayerFiles.Select(relative =>
            {
                var path = Path.Combine(player, relative.Replace('/', Path.DirectorySeparatorChar));
                return "{\"relative_path\":\"" + relative + "\",\"size_bytes\":" + new FileInfo(path).Length + ",\"sha256\":\"" + Sha256File(path) + "\"}";
            });
            return "{\"schema_version\":" + ReportSchemaVersion + ",\"input_sha256\":\"" + inputHash + "\",\"outputs\":[" + string.Join(",", outputs) + "]}";
        }

        private static string FailureCode(Exception failure)
        {
            if (failure == null) return "";
            if (failure is IOException) return "IO_ERROR";
            if (failure is ArgumentException) return "INVALID_ARGUMENT";
            return "BUILD_ERROR";
        }

        private static string SanitizeFailure(Exception failure)
        {
            if (failure == null) return "";
            return "Failure details redacted.";
        }

        public static string FormatBuildStepsForReport(IEnumerable<BuildStep> steps)
        {
            var lines = new List<string> { "name,duration_seconds" };
            if (steps != null)
            {
                foreach (var step in steps)
                {
                    lines.Add(Csv(step.name) + "," + step.duration.TotalSeconds.ToString(CultureInfo.InvariantCulture));
                }
            }

            return string.Join("\n", lines) + "\n";
        }

        public static string FormatPackedAssetsForReport(IEnumerable<string[]> rows)
        {
            var lines = new List<string> { "pack_file,source_asset_path,type,packed_size" };
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    if (row == null || row.Length != 4) continue;
                    lines.Add(Csv(row[0]) + "," + Csv(row[1]) + "," + Csv(row[2]) + "," + Csv(row[3]));
                }
            }

            return string.Join("\n", lines) + "\n";
        }

        private static IEnumerable<string[]> PackedAssetRows(BuildReport report)
        {
            if (report == null || report.packedAssets == null) yield break;

            foreach (var packedAssets in report.packedAssets)
            {
                if (packedAssets == null || packedAssets.contents == null) continue;
                foreach (var asset in packedAssets.contents)
                {
                    yield return new[]
                    {
                        packedAssets.shortPath ?? "",
                        asset.sourceAssetPath ?? "",
                        asset.type.ToString(),
                        asset.packedSize.ToString(CultureInfo.InvariantCulture)
                    };
                }
            }
        }

        private static string SceneDependencies(IEnumerable<string> scenes)
        {
            var lines = new List<string> { "scene,dependency" };
            foreach (var scene in scenes)
                foreach (var dependency in AssetDatabase.GetDependencies(scene, true)) lines.Add(Csv(scene) + "," + Csv(dependency));
            return string.Join("\n", lines) + "\n";
        }

        private static string Importers(IEnumerable<string> scenes)
        {
            var lines = new List<string> { "asset_path,importer_type" };
            var seen = new HashSet<string>();
            foreach (var scene in scenes)
                foreach (var dependency in AssetDatabase.GetDependencies(scene, true))
                    if (seen.Add(dependency))
                    {
                        var importer = AssetImporter.GetAtPath(dependency);
                        lines.Add(Csv(dependency) + "," + Csv(importer == null ? "" : importer.GetType().Name));
                    }
            return string.Join("\n", lines) + "\n";
        }

        private static string OutputFiles(string player)
        {
            var lines = new List<string> { "path,size_bytes,exists" };
            if (Directory.Exists(player)) foreach (var file in Directory.GetFiles(player, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(player.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                lines.Add(Csv(relativePath) + "," + new FileInfo(file).Length + ",true");
            }
            return string.Join("\n", lines) + "\n";
        }

        private static void WriteAllText(string directory, string name, string contents)
        {
            var trustedRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            var target = GetSafeReportTarget(trustedRoot, directory, name);
            var temporary = GetSafeReportTarget(trustedRoot, directory, "." + name + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush(true);
                }

                GetSafeReportTarget(trustedRoot, directory, name);
                if (PathExists(target)) DeleteFileEntrySafely(trustedRoot, target);
                File.Move(temporary, target);
            }
            finally
            {
                if (PathExists(temporary)) DeleteFileEntrySafely(trustedRoot, temporary);
            }
        }

        private static string GetSafeReportTarget(string trustedRoot, string directory, string name)
        {
            if (string.IsNullOrEmpty(name) || !string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal))
                throw new ArgumentException("Report file name is invalid.", nameof(name));
            EnsureNoReparsePoints(trustedRoot, directory);
            var target = Path.GetFullPath(Path.Combine(directory, name));
            if (!target.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Report file escapes candidate directory.");
            if (PathExists(target))
            {
                var attributes = File.GetAttributes(target);
                if (IsReparsePoint(attributes)) throw new InvalidOperationException("Report target is a reparse point.");
                if ((attributes & FileAttributes.Directory) != 0) throw new IOException("Report target is a directory.");
            }

            return target;
        }

        private static bool IsSafeExistingFile(string trustedRoot, string path)
        {
            if (!PathExists(path)) return false;
            EnsureNoReparsePoints(trustedRoot, path);
            var attributes = File.GetAttributes(path);
            return !IsReparsePoint(attributes) && (attributes & FileAttributes.Directory) == 0;
        }

        private static bool PathExists(string path)
        {
            try
            {
                File.GetAttributes(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static void DeleteFileEntrySafely(string trustedRoot, string path)
        {
            if (!PathExists(path)) return;
            EnsureNoReparsePoints(trustedRoot, path);
            var attributes = File.GetAttributes(path);
            if (IsReparsePoint(attributes) || (attributes & FileAttributes.Directory) != 0)
                throw new InvalidOperationException("Refusing to delete a reparse or directory entry as a file.");
            File.Delete(path);
        }

        private static string Sha256(string contents)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(contents ?? ""))).Replace("-", "");
        }

        private static string Sha256File(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
        }

        private static bool IsSha256(string value)
        {
            return !string.IsNullOrEmpty(value) && Regex.IsMatch(value, "^[A-F0-9]{64}$");
        }
        private static string JsonArray(IEnumerable<string> values) { return string.Join(",", values.Select(value => "\"" + Json(value) + "\"")); }
        private static string Json(string value) { return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
        private static string Csv(string value) { return "\"" + NeutralizeFormula(value).Replace("\"", "\"\"") + "\""; }

        private static string NeutralizeFormula(string value)
        {
            value = value ?? "";
            var trimmed = value.TrimStart(' ', '\t', '\r', '\n');
            return trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0 ? "'" + value : value;
        }

        private sealed class CandidateLease : IDisposable
        {
            private readonly string root;
            private readonly string directory;
            private readonly string lockPath;
            private FileStream stream;

            public CandidateLease(string root, string directory, string lockPath, FileStream stream)
            {
                this.root = root;
                this.directory = directory;
                this.lockPath = lockPath;
                this.stream = stream;
            }

            public void Dispose()
            {
                if (stream == null) return;
                stream.Dispose();
                stream = null;
                DeleteFileEntrySafely(root, lockPath);
            }
        }

        [Serializable]
        private sealed class BuildReportRecord
        {
            public int schema_version;
            public string tool_version;
            public string candidate;
            public string result;
            public bool release;
            public bool development;
            public bool profiler;
            public bool debugging;
            public int errors;
            public long total_size;
            public string input_sha256;
            public string manifest_sha256;
            public string failure_code;
            public string failure;
        }

        [Serializable]
        private sealed class ArtifactManifestRecord
        {
            public int schema_version;
            public string input_sha256;
            public OutputHashRecord[] outputs;
        }

        [Serializable]
        private sealed class OutputHashRecord
        {
            public string relative_path;
            public long size_bytes;
            public string sha256;
        }
    }
}
