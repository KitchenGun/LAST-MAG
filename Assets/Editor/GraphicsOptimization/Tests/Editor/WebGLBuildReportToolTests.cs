using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace GraphicsOptimization.Tests
{
    public sealed class WebGLBuildReportToolTests
    {
        [Test]
        public void CandidateValidation_AcceptsOnlySafeNames()
        {
            var validate = FindToolType().GetMethod("IsValidCandidate");

            Assert.That(validate, Is.Not.Null, "Expected candidate validation entrypoint.");
            Assert.That(Validate(validate, "baseline-release"), Is.True);
            Assert.That(Validate(validate, "A.1_test-2"), Is.True);
            Assert.That(Validate(validate, new string('a', 64)), Is.True);
            Assert.That(Validate(validate, ""), Is.False);
            Assert.That(Validate(validate, "../escape"), Is.False);
            Assert.That(Validate(validate, "folder/name"), Is.False);
            Assert.That(Validate(validate, "folder\\name"), Is.False);
            Assert.That(Validate(validate, new string('a', 65)), Is.False);
        }

        [Test]
        public void ReleaseBuildOptions_UseDetailedReportWithoutDevelopmentFlags()
        {
            var getOptions = FindToolType().GetMethod("GetReleaseBuildOptions");

            Assert.That(getOptions, Is.Not.Null, "Expected Release BuildOptions entrypoint.");
            var options = (BuildOptions)getOptions.Invoke(null, null);
            Assert.That(options.HasFlag(BuildOptions.DetailedBuildReport), Is.True);
            Assert.That(options.HasFlag(BuildOptions.Development), Is.False);
            Assert.That(options.HasFlag(BuildOptions.ConnectWithProfiler), Is.False);
            Assert.That(options.HasFlag(BuildOptions.AllowDebugging), Is.False);
        }

        [Test]
        public void EnabledScenes_ComeFromEditorBuildSettings()
        {
            var getScenes = FindToolType().GetMethod("GetEnabledScenePaths");

            Assert.That(getScenes, Is.Not.Null, "Expected enabled-scene entrypoint.");
            var scenes = (string[])getScenes.Invoke(null, null);
            CollectionAssert.AreEqual(EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray(), scenes);
        }

        [Test]
        public void CandidateOutputDirectory_RemainsInsideGraphicsBuildRoot()
        {
            var getOutputDirectory = FindToolType().GetMethod("GetCandidateOutputDirectory");

            Assert.That(getOutputDirectory, Is.Not.Null, "Expected safe output-directory entrypoint.");
            var path = (string)getOutputDirectory.Invoke(null, new object[] { "baseline-release" });
            StringAssert.Contains("Builds", path);
            StringAssert.Contains("GraphicsOptimization", path);
            StringAssert.EndsWith("baseline-release", path.Replace('\\', '/'));
            Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                getOutputDirectory.Invoke(null, new object[] { "../escape" }));
        }

        [Test]
        public void CandidateBuildLease_UsesAnExclusiveFilesystemLock()
        {
            var toolType = FindToolType();
            var acquire = toolType.GetMethod("TryAcquireCandidateLease");
            var directory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-Lease-" + Guid.NewGuid().ToString("N"));

            Assert.That(acquire, Is.Not.Null, "Expected cross-process candidate lease entrypoint.");
            try
            {
                Directory.CreateDirectory(directory);
                var first = (IDisposable)acquire.Invoke(null, new object[] { directory, directory, "lease-test" });
                var second = acquire.Invoke(null, new object[] { directory, directory, "lease-test" });

                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.Null, "Independent acquisition must fail while the lock file exists.");
                first.Dispose();

                var third = (IDisposable)acquire.Invoke(null, new object[] { directory, directory, "lease-test" });
                Assert.That(third, Is.Not.Null);
                third.Dispose();

                Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    acquire.Invoke(null, new object[] { directory, directory + "-escape", "lease-test" }));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CandidateBuildLease_RejectsSeparateProcessContention_ThenReleases()
        {
            var acquire = FindToolType().GetMethod("TryAcquireCandidateLease");
            var directory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-ProcessLease-" + Guid.NewGuid().ToString("N"));
            var lockPath = Path.Combine(directory, ".webgl-build.lock");

            try
            {
                Directory.CreateDirectory(directory);
                using (var holder = StartExternalLockHolder(lockPath))
                {
                    Assert.That(holder, Is.Not.Null);
                    Assert.That(holder.StandardOutput.ReadLine(), Is.EqualTo("ready"));
                    Assert.That(acquire.Invoke(null, new object[] { directory, directory, "lease-test" }), Is.Null);
                    Assert.That(holder.WaitForExit(10000), Is.True);
                    Assert.That(holder.ExitCode, Is.EqualTo(0));
                }

                var lease = (IDisposable)acquire.Invoke(null, new object[] { directory, directory, "lease-test" });
                Assert.That(lease, Is.Not.Null);
                lease.Dispose();
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CandidateBuildLease_RequiresManualStaleLockRemoval_AndReleasesAfterFailure()
        {
            var acquire = FindToolType().GetMethod("TryAcquireCandidateLease");
            var directory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-StaleLease-" + Guid.NewGuid().ToString("N"));
            var lockPath = Path.Combine(directory, ".webgl-build.lock");

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(lockPath, "stale");
                Assert.That(acquire.Invoke(null, new object[] { directory, directory, "lease-test" }), Is.Null);
                File.Delete(lockPath);

                IDisposable lease = null;
                try
                {
                    lease = (IDisposable)acquire.Invoke(null, new object[] { directory, directory, "lease-test" });
                    Assert.That(lease, Is.Not.Null);
                    throw new IOException("simulated build failure");
                }
                catch (IOException)
                {
                }
                finally
                {
                    if (lease != null) lease.Dispose();
                }

                var recovered = (IDisposable)acquire.Invoke(null, new object[] { directory, directory, "lease-test" });
                Assert.That(recovered, Is.Not.Null);
                recovered.Dispose();
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CandidateBuildLease_PropagatesNonContentionIoFailure()
        {
            var acquire = FindToolType().GetMethod("TryAcquireCandidateLease");
            var directory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-IoLease-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(Path.Combine(directory, ".webgl-build.lock"));
                Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    acquire.Invoke(null, new object[] { directory, directory, "lease-test" }));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void BuildStepsFormatter_WritesOneCsvRowPerBuildStep()
        {
            var format = FindToolType().GetMethod("FormatBuildStepsForReport");

            Assert.That(format, Is.Not.Null, "Expected BuildReport step CSV formatter.");
            var csv = (string)format.Invoke(null, new object[] { new[] { new BuildStep(), new BuildStep() } });

            Assert.That(csv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length, Is.EqualTo(3));
        }

        [Test]
        public void PackedAssetsFormatter_WritesSyntheticPackedAssetRows()
        {
            var format = FindToolType().GetMethod("FormatPackedAssetsForReport");
            var rows = new List<string[]>
            {
                new[] { "Build/archive.data", "Assets/Art/A, B.prefab", "SerializedFile", "123" }
            };

            Assert.That(format, Is.Not.Null, "Expected packed-asset CSV formatter.");
            var csv = (string)format.Invoke(null, new object[] { rows });

            StringAssert.Contains("pack_file,source_asset_path,type,packed_size", csv);
            StringAssert.Contains("\"Build/archive.data\",\"Assets/Art/A, B.prefab\",\"SerializedFile\",\"123\"", csv);
        }

        [Test]
        public void CsvFormatter_NeutralizesFormulaLeadingCells()
        {
            var format = FindToolType().GetMethod("FormatPackedAssetsForReport");
            var rows = new List<string[]>
            {
                new[] { "=SUM(1,1)", "\t+formula", "-danger", "123" },
                new[] { "@name", "Assets/valid.prefab", "SerializedFile", "456" }
            };

            Assert.That(format, Is.Not.Null, "Expected CSV formatter.");
            var csv = (string)format.Invoke(null, new object[] { rows });

            StringAssert.Contains("\"'=SUM(1,1)\"", csv);
            StringAssert.Contains("\"'\t+formula\"", csv);
            StringAssert.Contains("\"'-danger\"", csv);
            StringAssert.Contains("\"'@name\"", csv);
        }

        [Test]
        public void PackedAssetsFormatter_NeutralizesFormulaInEveryCell()
        {
            var format = FindToolType().GetMethod("FormatPackedAssetsForReport");
            var csv = (string)format.Invoke(null, new object[] { new[] { new[] { "pack", "asset", "type", "=1+1" } } });

            StringAssert.Contains("\"'=1+1\"", csv);
        }

        [Test]
        public void ReportWrite_ReplacesFileLinkEntry_WithoutMutatingOutsideSentinel()
        {
            var write = FindToolType().GetMethod("WriteAllText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var directory = CandidateDirectory("r9-report-link");
            var outsideDirectory = CandidateDirectory("r9-report-sentinel");
            var outside = Path.Combine(outsideDirectory, "sentinel.txt");
            var target = Path.Combine(directory, "build-report.json");

            Assert.That(write, Is.Not.Null);
            try
            {
                Directory.CreateDirectory(directory);
                Directory.CreateDirectory(outsideDirectory);
                File.WriteAllText(outside, "preserve");
                CreateHardLinkOrFail(target, outside);

                Assert.DoesNotThrow(() =>
                    write.Invoke(null, new object[] { directory, "build-report.json", "replacement" }));
                Assert.That(File.ReadAllText(target), Is.EqualTo("replacement"));
                Assert.That(File.ReadAllText(outside), Is.EqualTo("preserve"));
            }
            finally
            {
                if (File.Exists(target)) File.Delete(target);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                if (Directory.Exists(outsideDirectory)) Directory.Delete(outsideDirectory, true);
            }
        }

        [Test]
        public void ReportWrite_RejectsReparseNamedReportTarget_AndPreservesOutsideSentinel()
        {
            var write = FindToolType().GetMethod("WriteAllText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var directory = CandidateDirectory("r9-report-reparse");
            var outside = CandidateDirectory("r9-report-reparse-sentinel");
            var target = Path.Combine(directory, "build-report.json");
            var sentinel = Path.Combine(outside, "sentinel.txt");

            try
            {
                Directory.CreateDirectory(directory);
                Directory.CreateDirectory(outside);
                File.WriteAllText(sentinel, "preserve");
                Assert.That(TryCreateDirectoryLink(target, outside), Is.True);

                var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    write.Invoke(null, new object[] { directory, "build-report.json", "replacement" }));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("preserve"));
            }
            finally
            {
                if (Directory.Exists(target)) Directory.Delete(target);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                if (Directory.Exists(outside)) Directory.Delete(outside, true);
            }
        }

        [Test]
        public void CompletedCandidateValidation_RejectsSchemaOneReportWithoutInputAndOutputIntegrity()
        {
            var validate = FindToolType().GetMethod("IsValidCompletedCandidate");
            var directory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-Replay-" + Guid.NewGuid().ToString("N"));

            try
            {
                CreateCompletedPlayer(directory);
                File.WriteAllText(Path.Combine(directory, "build-report.json"), SuccessfulReport("replay-test", 5));

                Assert.That((bool)validate.Invoke(null, new object[] { directory, "replay-test" }), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CompletedCandidateValidation_RejectsTamperedInputOrSameSizeOutput()
        {
            var validate = FindToolType().GetMethod("IsValidCompletedCandidate");
            var directory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-Integrity-" + Guid.NewGuid().ToString("N"));

            try
            {
                CreateVerifiedCompletedCandidate(directory, "integrity-test");
                Assert.That((bool)validate.Invoke(null, new object[] { directory, "integrity-test" }), Is.True);

                var output = Path.Combine(directory, "Player", "Build", "Player.data.unityweb");
                File.WriteAllText(output, "y");
                File.SetLastWriteTimeUtc(Path.Combine(directory, "build-report.json"), DateTime.UtcNow.AddSeconds(1));
                Assert.That((bool)validate.Invoke(null, new object[] { directory, "integrity-test" }), Is.False);

                CreateVerifiedCompletedCandidate(directory, "integrity-test");
                File.AppendAllText(Path.Combine(directory, "build-inputs.json"), " ");
                Assert.That((bool)validate.Invoke(null, new object[] { directory, "integrity-test" }), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void FailedReport_SanitizesAbsolutePath_AndKeepsErrorCategory()
        {
            var writeReports = FindToolType().GetMethod("WriteReports", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var directory = CandidateDirectory("r9-failure");
            var exposedPath = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-SecretPath");
            const string forwardSlashDrivePath = "D:/outside/secret.txt";
            const string unrelatedUncPath = "\\\\server\\share\\secret.txt";

            Assert.That(writeReports, Is.Not.Null);
            try
            {
                Directory.CreateDirectory(directory);
                writeReports.Invoke(null, new object[] { directory, "r9-failure", new string[0], BuildOptions.DetailedBuildReport, null, new IOException("Unable to write " + exposedPath + "; " + forwardSlashDrivePath + "; " + unrelatedUncPath) });
                var report = File.ReadAllText(Path.Combine(directory, "build-report.json"));

                StringAssert.DoesNotContain(exposedPath, report);
                StringAssert.DoesNotContain(forwardSlashDrivePath, report);
                StringAssert.DoesNotContain(unrelatedUncPath, report);
                StringAssert.DoesNotContain("outside/secret.txt", report);
                StringAssert.DoesNotContain("share\\secret.txt", report);
                StringAssert.Contains("\"failure_code\":\"IO_ERROR\"", report);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void FailedReport_SanitizesUncOnlyFailure_AndKeepsErrorCategory()
        {
            var writeReports = FindToolType().GetMethod("WriteReports", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var directory = CandidateDirectory("r11-unc-failure");
            const string unrelatedUncPath = "\\\\server\\share\\secret.txt";

            Assert.That(writeReports, Is.Not.Null);
            try
            {
                Directory.CreateDirectory(directory);
                writeReports.Invoke(null, new object[] { directory, "r11-unc-failure", new string[0], BuildOptions.DetailedBuildReport, null, new IOException("Unable to write " + unrelatedUncPath) });
                var report = File.ReadAllText(Path.Combine(directory, "build-report.json"));

                StringAssert.DoesNotContain(unrelatedUncPath, report);
                StringAssert.DoesNotContain("share\\secret.txt", report);
                StringAssert.Contains("\"failure_code\":\"IO_ERROR\"", report);
                StringAssert.Contains("\"failure\":\"Failure details redacted.\"", report);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void ReparsePointCleanup_LeavesOutsideSentinelUntouched_WhenSymbolicLinksAreAvailable()
        {
            var deletePlayer = FindToolType().GetMethod("DeletePlayerDirectorySafely",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var baseDirectory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-Reparse-" + Guid.NewGuid().ToString("N"));
            var root = Path.Combine(baseDirectory, "root");
            var outside = Path.Combine(baseDirectory, "outside");
            var candidate = Path.Combine(root, "candidate");
            var sentinel = Path.Combine(outside, "Player", "sentinel.txt");

            Assert.That(deletePlayer, Is.Not.Null);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sentinel));
                File.WriteAllText(sentinel, "preserve");
                Directory.CreateDirectory(root);
                if (!TryCreateDirectoryLink(candidate, outside)) Assert.Ignore("Junction/symbolic-link creation is unavailable for this Windows test account.");

                Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    deletePlayer.Invoke(null, new object[] { root, candidate, Path.Combine(candidate, "Player") }));
                Assert.That(File.Exists(sentinel), Is.True);
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("preserve"));
            }
            finally
            {
                if (Directory.Exists(candidate)) Directory.Delete(candidate);
                if (Directory.Exists(root)) Directory.Delete(root);
                if (Directory.Exists(outside)) Directory.Delete(outside, true);
                if (Directory.Exists(baseDirectory)) Directory.Delete(baseDirectory);
            }
        }

        [Test]
        public void ReparsePointCleanup_RejectsDescendantJunction_AndPreservesOutsideSentinel()
        {
            var deletePlayer = FindToolType().GetMethod("DeletePlayerDirectorySafely",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var baseDirectory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-DescendantReparse-" + Guid.NewGuid().ToString("N"));
            var root = Path.Combine(baseDirectory, "root");
            var candidate = Path.Combine(root, "candidate");
            var player = Path.Combine(candidate, "Player");
            var outside = Path.Combine(baseDirectory, "outside");
            var sentinel = Path.Combine(outside, "sentinel.txt");

            try
            {
                Directory.CreateDirectory(player);
                Directory.CreateDirectory(outside);
                File.WriteAllText(sentinel, "preserve");
                Assert.That(TryCreateDirectoryLink(Path.Combine(player, "linked"), outside), Is.True);

                Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    deletePlayer.Invoke(null, new object[] { root, candidate, player }));
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("preserve"));
            }
            finally
            {
                var link = Path.Combine(player, "linked");
                if (Directory.Exists(link)) Directory.Delete(link);
                if (Directory.Exists(baseDirectory)) Directory.Delete(baseDirectory, true);
            }
        }

        private static bool TryCreateDirectoryLink(string link, string target)
        {
            var createLink = typeof(Directory).GetMethod("CreateSymbolicLink", new[] { typeof(string), typeof(string) });
            if (createLink != null)
            {
                try
                {
                    createLink.Invoke(null, new object[] { link, target });
                    return true;
                }
                catch (System.Reflection.TargetInvocationException exception) when
                    (exception.InnerException is UnauthorizedAccessException || exception.InnerException is IOException)
                {
                }
            }

            try
            {
                using (var command = Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                    Arguments = "/c mklink /J \"" + link + "\" \"" + target + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    command.WaitForExit();
                    return command.ExitCode == 0 && Directory.Exists(link)
                        && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string CandidateDirectory(string prefix)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Builds", "GraphicsOptimization", prefix + "-" + Guid.NewGuid().ToString("N"));
        }

        private static void CreateHardLinkOrFail(string link, string target)
        {
            Assert.That(CreateHardLink(link, target, IntPtr.Zero), Is.True,
                "Windows hard-link creation failed with error " + Marshal.GetLastWin32Error() + ".");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

        private static Process StartExternalLockHolder(string lockPath)
        {
            var escaped = lockPath.Replace("'", "''");
            return Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"$s=[System.IO.File]::Open('" + escaped + "',[System.IO.FileMode]::CreateNew,[System.IO.FileAccess]::ReadWrite,[System.IO.FileShare]::None);[Console]::Out.WriteLine('ready');Start-Sleep -Milliseconds 1500;$s.Dispose();Remove-Item -LiteralPath '" + escaped + "'\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
        }

        private static void CreateVerifiedCompletedCandidate(string directory, string candidate)
        {
            CreateCompletedPlayer(directory);
            var tool = FindToolType();
            var inputs = (string)tool.GetMethod("BuildInputsJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { candidate, WebGLBuildReportTool.GetEnabledScenePaths(), WebGLBuildReportTool.GetReleaseBuildOptions() });
            var inputHash = Sha256(inputs);
            var manifest = (string)tool.GetMethod("ArtifactManifestJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { inputHash, Path.Combine(directory, "Player") });
            var manifestHash = Sha256(manifest);
            File.WriteAllText(Path.Combine(directory, "build-inputs.json"), inputs);
            File.WriteAllText(Path.Combine(directory, "artifact-manifest.json"), manifest);
            File.WriteAllText(Path.Combine(directory, "build-report.json"), "{\"schema_version\":2,\"tool_version\":\"2\",\"candidate\":\"" + candidate + "\",\"result\":\"Succeeded\",\"release\":true,\"development\":false,\"profiler\":false,\"debugging\":false,\"errors\":0,\"warnings\":0,\"total_size\":5,\"input_sha256\":\"" + inputHash + "\",\"manifest_sha256\":\"" + manifestHash + "\",\"failure_code\":\"\",\"failure\":\"\"}");
        }

        private static string Sha256(string contents)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(contents))).Replace("-", "");
        }

        [Test]
        public void CompletedCandidateValidation_RejectsMalformedWrongCandidateAndPartialPlayer()
        {
            var validate = FindToolType().GetMethod("IsValidCompletedCandidate");
            var candidate = "validation-test";
            var directory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-" + Guid.NewGuid().ToString("N"));

            Assert.That(validate, Is.Not.Null, "Expected structured completed-candidate validator.");
            try
            {
                CreateVerifiedCompletedCandidate(directory, candidate);

                Assert.That((bool)validate.Invoke(null, new object[] { directory, candidate }), Is.True);

                File.WriteAllText(Path.Combine(directory, "build-report.json"), "{not-json");
                Assert.That((bool)validate.Invoke(null, new object[] { directory, candidate }), Is.False);

                CreateVerifiedCompletedCandidate(directory, candidate);
                File.WriteAllText(Path.Combine(directory, "build-report.json"), File.ReadAllText(Path.Combine(directory, "build-report.json")).Replace(candidate, "wrong-candidate"));
                Assert.That((bool)validate.Invoke(null, new object[] { directory, candidate }), Is.False);

                CreateVerifiedCompletedCandidate(directory, candidate);
                File.Delete(Path.Combine(directory, "Player", "Build", "Player.wasm.unityweb"));
                Assert.That((bool)validate.Invoke(null, new object[] { directory, candidate }), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CompletedCandidateValidation_PreservesAlternateCandidateCasing()
        {
            var validate = FindToolType().GetMethod("IsValidCompletedCandidate");
            var directory = Path.Combine(Path.GetTempPath(), "GraphicsOptimization-Casing-" + Guid.NewGuid().ToString("N"));

            Assert.That(validate, Is.Not.Null);
            try
            {
                CreateVerifiedCompletedCandidate(directory, "release-a");

                Assert.That((bool)validate.Invoke(null, new object[] { directory, "Release-A" }), Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static Type FindToolType()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("GraphicsOptimization.WebGLBuildReportTool", false))
                .FirstOrDefault(type => type != null);
        }

        private static bool Validate(System.Reflection.MethodInfo method, string candidate)
        {
            return (bool)method.Invoke(null, new object[] { candidate });
        }

        private static string SuccessfulReport(string candidate, int totalSize)
        {
            return "{\"schema_version\":1,\"tool_version\":\"1\",\"candidate\":\"" + candidate + "\",\"result\":\"Succeeded\",\"release\":true,\"development\":false,\"profiler\":false,\"debugging\":false,\"errors\":0,\"warnings\":0,\"total_size\":" + totalSize + ",\"failure\":\"\"}";
        }

        private static void CreateCompletedPlayer(string directory)
        {
            Directory.CreateDirectory(Path.Combine(directory, "Player", "Build"));
            File.WriteAllText(Path.Combine(directory, "Player", "index.html"), "x");
            File.WriteAllText(Path.Combine(directory, "Player", "Build", "Player.data.unityweb"), "x");
            File.WriteAllText(Path.Combine(directory, "Player", "Build", "Player.framework.js.unityweb"), "x");
            File.WriteAllText(Path.Combine(directory, "Player", "Build", "Player.loader.js"), "x");
            File.WriteAllText(Path.Combine(directory, "Player", "Build", "Player.wasm.unityweb"), "x");
        }
    }
}
