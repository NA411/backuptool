using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ConsoleUITests
{
    [TestClass]
    public class UITests
    {
        private string _testRootDirectory = null!;
        private string _sourceDirectory = null!;
        private string _restoreDirectory = null!;
        private string _backupToolPath = null!;
        private string _databasePath = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create test directories
            _testRootDirectory = Path.Combine(Path.GetTempPath(), "BackupToolUITests", Guid.NewGuid().ToString());
            _sourceDirectory = Path.Combine(_testRootDirectory, "Source");
            _restoreDirectory = Path.Combine(_testRootDirectory, "Restore");

            Directory.CreateDirectory(_testRootDirectory);
            Directory.CreateDirectory(_sourceDirectory);
            Directory.CreateDirectory(_restoreDirectory);

            // Set database path in test directory
            _databasePath = Path.Combine(_testRootDirectory, "backup.db");

            // Find the backup tool executable
            // Assumes the test is running from the test project directory
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionRoot = FindSolutionRoot(currentDirectory);
            _backupToolPath = Path.Combine(solutionRoot, "src", "backuptool.console", "bin", "Debug", "net8.0", "backuptool.exe");

            // If .exe doesn't exist, try without extension (for Linux/Mac)
            if (!File.Exists(_backupToolPath))
            {
                _backupToolPath = Path.Combine(solutionRoot, "src", "backuptool.console", "bin", "Debug", "net8.0", "backuptool");
            }

            // If still not found, build the project
            if (!File.Exists(_backupToolPath))
            {
                BuildProject(solutionRoot);
            }

            Assert.IsTrue(File.Exists(_backupToolPath), $"Backup tool executable not found at: {_backupToolPath}");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testRootDirectory))
            {
                Directory.Delete(_testRootDirectory, true);
            }
        }

        #region Snapshot Command Tests

        [TestMethod]
        public async Task UI_Snapshot_WhenValidDirectory_CreatesSnapshotSuccessfully()
        {
            // Arrange
            await CreateTestFiles();

            // Act
            var result = await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");

            // Assert
            Assert.AreEqual(0, result.ExitCode, $"Snapshot command failed. Output: {result.Output}, Error: {result.Error}");
            Assert.IsTrue(result.Output.Contains("Snapshot") && result.Output.Contains("created successfully"),
                "Success message not found in output");
            Assert.IsTrue(File.Exists(_databasePath), "Database file was not created");
        }

        [TestMethod]
        public async Task UI_Snapshot_WhenVerboseFlag_ShowsDetailedOutput()
        {
            // Arrange
            await CreateTestFiles();

            // Act
            var result = await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\" --verbose");

            // Assert
            Assert.AreEqual(0, result.ExitCode, "Snapshot command with verbose flag failed");
            Assert.IsTrue(result.Output.Contains("Creating snapshot"), "Verbose output not found");
            Assert.IsTrue(result.Output.Contains("Files:") && result.Output.Contains("Bytes:"),
                "File and byte statistics not found in verbose output");
        }

        [TestMethod]
        public async Task UI_Snapshot_WhenDirectoryDoesNotExist_ShowsError()
        {
            // Arrange
            var nonExistentDir = Path.Combine(_testRootDirectory, "NonExistent");

            // Act
            var result = await RunBackupToolAsync($"snapshot --target-directory \"{nonExistentDir}\"");

            // Assert
            Assert.AreNotEqual(0, result.ExitCode, "Should fail when directory doesn't exist");
            Assert.IsTrue(result.Error.Contains("does not exist") || result.Output.Contains("does not exist"), "Error message about non-existent directory not found");
        }

        [TestMethod]
        public async Task UI_Snapshot_WhenMissingRequiredArgument_ShowsUsageHelp()
        {
            // Act
            var result = await RunBackupToolAsync("snapshot");

            // Assert
            Assert.AreNotEqual(0, result.ExitCode, "Should fail when required argument is missing");
            Assert.IsTrue(result.Error.Contains("target-directory") || result.Output.Contains("target-directory"),"Help message about required argument not found");
        }

        #endregion

        #region List Command Tests

        [TestMethod]
        public async Task UI_List_WhenNoSnapshots_ShowsEmptyList()
        {
            // Act
            var result = await RunBackupToolAsync("list");

            // Assert
            Assert.AreEqual(0, result.ExitCode, "List command should succeed even with no snapshots");
            Assert.IsTrue(result.Output.Contains("SNAPSHOT") && result.Output.Contains("TIMESTAMP"),
                "List header not found");
        }

        [TestMethod]
        public async Task UI_List_WhenSnapshotsExist_ShowsSnapshotDetails()
        {
            // Arrange
            await CreateTestFiles();
            await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");

            // Act
            var result = await RunBackupToolAsync("list");

            // Assert
            Assert.AreEqual(0, result.ExitCode, "List command failed");
            Assert.IsTrue(result.Output.Contains("SNAPSHOT") && result.Output.Contains("TIMESTAMP"),
                "List header not found");
            Assert.IsTrue(result.Output.Contains('1'), "Snapshot ID not found in list");
            Assert.IsTrue(result.Output.Contains("total"), "Total summary not found");
        }

        [TestMethod]
        public async Task UI_List_WhenMultipleSnapshots_ShowsAllSnapshots()
        {
            // Arrange
            await CreateTestFiles();
            await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");

            // Create second snapshot with different content
            await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "additional.txt"), "Additional content");
            await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");

            // Act
            var result = await RunBackupToolAsync("list");

            // Assert
            Assert.AreEqual(0, result.ExitCode, "List command failed");
            Assert.IsTrue(result.Output.Contains('1') && result.Output.Contains('2'), "Both snapshot IDs not found in list");
        }

        #endregion

        #region Restore Command Tests

        [TestMethod]
        public async Task UI_Restore_WhenValidSnapshot_RestoresFilesSuccessfully()
        {
            // Arrange
            await CreateTestFiles();
            var snapshotResult = await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");
            Assert.AreEqual(0, snapshotResult.ExitCode, "Setup snapshot failed");

            // Act
            var result = await RunBackupToolAsync($"restore --snapshot-number 1 --output-directory \"{_restoreDirectory}\"");

            // Assert
            Assert.AreEqual(0, result.ExitCode, $"Restore command failed. Output: {result.Output}, Error: {result.Error}");
            Assert.IsTrue(result.Output.Contains("Restore completed successfully"),
                "Success message not found");

            // Verify files were restored
            Assert.IsTrue(File.Exists(Path.Combine(_restoreDirectory, "test1.txt")), "test1.txt not restored");
            Assert.IsTrue(File.Exists(Path.Combine(_restoreDirectory, "test2.bin")), "test2.bin not restored");
            Assert.IsTrue(File.Exists(Path.Combine(_restoreDirectory, "subfolder", "nested.txt")), "nested.txt not restored");
        }

        [TestMethod]
        public async Task UI_Restore_WhenCreateDirectoryFlag_CreatesOutputDirectory()
        {
            // Arrange
            await CreateTestFiles();
            var snapshotResult = await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");
            Assert.AreEqual(0, snapshotResult.ExitCode, "Setup snapshot failed");

            var newRestoreDir = Path.Combine(_testRootDirectory, "NewRestore");

            // Act
            var result = await RunBackupToolAsync($"restore --snapshot-number 1 --output-directory \"{newRestoreDir}\" --create-directory");

            // Assert
            Assert.AreEqual(0, result.ExitCode, "Restore with create-directory failed");
            Assert.IsTrue(Directory.Exists(newRestoreDir), "Output directory was not created");
            Assert.IsTrue(File.Exists(Path.Combine(newRestoreDir, "test1.txt")), "File not restored to new directory");
        }

        [TestMethod]
        public async Task UI_Restore_WhenSnapshotDoesNotExist_ShowsError()
        {
            // Act
            var result = await RunBackupToolAsync($"restore --snapshot-number 999 --output-directory \"{_restoreDirectory}\"");

            // Assert
            Assert.AreNotEqual(0, result.ExitCode, "Should fail when snapshot doesn't exist");
            Assert.IsTrue(result.Error.Contains("Snapshot: 999 does not exist") || result.Output.Contains("Snapshot: 999 does not exist"), "Error message about non-existent snapshot not found");
        }

        [TestMethod]
        public async Task UI_Restore_WhenOutputDirectoryDoesNotExist_ShowsError()
        {
            // Arrange
            await CreateTestFiles();
            await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");
            var nonExistentDir = Path.Combine(_testRootDirectory, "NonExistentRestore");

            // Act
            var result = await RunBackupToolAsync($"restore --snapshot-number 1 --output-directory \"{nonExistentDir}\"");

            // Assert
            Assert.AreNotEqual(0, result.ExitCode, "Should fail when output directory doesn't exist");
            Assert.IsTrue(result.Error.Contains("does not exist") || result.Output.Contains("does not exist"), "Error message about non-existent output directory not found");
        }

        #endregion

        #region Prune Command Tests

        [TestMethod]
        public async Task UI_Prune_WhenValidSnapshot_RemovesSnapshotSuccessfully()
        {
            // Arrange
            await CreateTestFiles();
            await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");

            // Verify snapshot exists
            var listBefore = await RunBackupToolAsync("list");
            Assert.IsTrue(listBefore.Output.Contains('1'), "Setup snapshot not found");

            // Act
            var result = await RunBackupToolAsync("prune --snapshot 1");

            // Assert
            Assert.AreEqual(0, result.ExitCode, $"Prune command failed. Output: {result.Output}, Error: {result.Error}");
            Assert.IsTrue(result.Output.Contains("Prune completed successfully"),
                "Success message not found");

            // Verify snapshot was removed
            var listAfter = await RunBackupToolAsync("list");
            Assert.IsFalse(listAfter.Output.Contains("1       "), "Snapshot still exists after pruning");
        }

        [TestMethod]
        public async Task UI_Prune_WhenSnapshotDoesNotExist_ShowsError()
        {
            // Act
            var result = await RunBackupToolAsync("prune --snapshot 999");

            // Assert
            Assert.AreNotEqual(0, result.ExitCode, "Should fail when snapshot doesn't exist");
        }

        [TestMethod]
        public async Task UI_Prune_WhenMultipleSnapshotsExist_RemovesOnlySpecified()
        {
            // Arrange
            await CreateTestFiles();
            await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");

            // Add file and create second snapshot
            await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "additional.txt"), "Additional");
            await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");

            // Act
            var result = await RunBackupToolAsync("prune --snapshot 1");

            // Assert
            Assert.AreEqual(0, result.ExitCode, "Prune command failed");

            var listAfter = await RunBackupToolAsync("list");
            Assert.IsFalse(listAfter.Output.Contains("1       "), "Snapshot 1 still exists");
            Assert.IsTrue(listAfter.Output.Contains('2'), "Snapshot 2 was incorrectly removed");
        }

        #endregion

        #region Check Command Tests

        [TestMethod]
        public async Task UI_Check_WhenNoCorruption_ShowsNoIssues()
        {
            // Arrange
            await CreateTestFiles();
            await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");

            // Act
            var result = await RunBackupToolAsync("check");

            // Assert
            Assert.AreEqual(0, result.ExitCode, $"Check command failed. Output: {result.Output}, Error: {result.Error}");
            Assert.IsTrue(result.Output.Contains("No corrupted file content found"), "No corrupted file content found.");
        }

        [TestMethod]
        public async Task UI_Check_WhenNoSnapshots_ShowsNoIssues()
        {
            // Act
            var result = await RunBackupToolAsync("check");

            // Assert
            Assert.AreEqual(0, result.ExitCode, "Check command should succeed with no snapshots");
            Assert.IsTrue(result.Output.Contains("No corrupted file content found"), "No corrupted file content found.");
        }

        #endregion

        #region Help and Usage Tests

        [TestMethod]
        public async Task UI_Help_WhenNoArguments_ShowsUsageInformation()
        {
            // Act
            var result = await RunBackupToolAsync("");

            // Assert
            Assert.IsTrue(result.Output.Contains("BACKUP TOOL") || result.Output.Contains("backup tool"),
                "Tool name not found in help");
            Assert.IsTrue(result.Output.Contains("snapshot") && result.Output.Contains("restore") &&
                         result.Output.Contains("list") && result.Output.Contains("prune") &&
                         result.Output.Contains("check"),
                "All commands not listed in help");
        }

        [TestMethod]
        public async Task UI_Help_WhenInvalidCommand_ShowsError()
        {
            // Act
            var result = await RunBackupToolAsync("invalid-command");

            // Assert
            Assert.AreNotEqual(0, result.ExitCode, "Should fail with invalid command");
        }

        [TestMethod]
        public async Task UI_Version_WhenVersionRequested_ShowsVersionInfo()
        {
            // Act
            var result = await RunBackupToolAsync("--version");

            // Assert
            // Note: Depending on how version is implemented, this might need adjustment
            Assert.IsTrue(result.ExitCode == 0 || result.Output.Contains("version") || result.Error.Contains("version"),
                "Version information not found");
        }

        #endregion

        #region Integration Workflow Tests

        [TestMethod]
        public async Task UI_CompleteWorkflow_WhenAllCommands_WorksTogether()
        {
            // Arrange
            await CreateTestFiles();

            // Act & Assert - Complete workflow

            // 1. Create snapshot
            var snapshotResult = await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\" --verbose");
            Assert.AreEqual(0, snapshotResult.ExitCode, "Snapshot creation failed");
            Assert.IsTrue(snapshotResult.Output.Contains("Snapshot") && snapshotResult.Output.Contains("created"), "Snapshot creation message not found");

            // 2. List snapshots
            var listResult = await RunBackupToolAsync("list");
            Assert.AreEqual(0, listResult.ExitCode, "List command failed");
            Assert.IsTrue(listResult.Output.Contains('1'), "Snapshot not found in list");

            // 3. Check for corruption
            var checkResult = await RunBackupToolAsync("check");
            Assert.AreEqual(0, checkResult.ExitCode, "Check command failed");
            Assert.IsTrue(checkResult.Output.Contains("No corrupted"), "Corruption check failed");

            // 4. Restore snapshot
            var restoreResult = await RunBackupToolAsync($"restore --snapshot-number 1 --output-directory \"{_restoreDirectory}\"");
            Assert.AreEqual(0, restoreResult.ExitCode, "Restore command failed");
            Assert.IsTrue(restoreResult.Output.Contains("Restore completed"), "Restore completion message not found");

            // 5. Verify restored files
            Assert.IsTrue(File.Exists(Path.Combine(_restoreDirectory, "test1.txt")), "Restored file not found");
            var restoredContent = await File.ReadAllTextAsync(Path.Combine(_restoreDirectory, "test1.txt"));
            Assert.AreEqual("Test file 1 content", restoredContent, "Restored content doesn't match");

            // 6. Create second snapshot
            await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "new_file.txt"), "New content");
            var snapshot2Result = await RunBackupToolAsync($"snapshot --target-directory \"{_sourceDirectory}\"");
            Assert.AreEqual(0, snapshot2Result.ExitCode, "Second snapshot creation failed");

            // 7. List both snapshots
            var list2Result = await RunBackupToolAsync("list");
            Assert.AreEqual(0, list2Result.ExitCode, "Second list command failed");
            Assert.IsTrue(list2Result.Output.Contains('1') && list2Result.Output.Contains('2'), "Both snapshots not found in list");

            // 8. Prune first snapshot
            var pruneResult = await RunBackupToolAsync("prune --snapshot 1");
            Assert.AreEqual(0, pruneResult.ExitCode, "Prune command failed");
            Assert.IsTrue(pruneResult.Output.Contains("Prune completed"), "Prune completion message not found");

            // 9. Verify only second snapshot remains
            var finalListResult = await RunBackupToolAsync("list");
            Assert.AreEqual(0, finalListResult.ExitCode, "Final list command failed");
            Assert.IsTrue(finalListResult.Output.Contains('2'), "Second snapshot not found after prune");
            Assert.IsFalse(finalListResult.Output.Contains("1       "), "First snapshot still exists after prune");

            // 10. Final check
            var finalCheckResult = await RunBackupToolAsync("check");
            Assert.AreEqual(0, finalCheckResult.ExitCode, "Final check command failed");
            Assert.IsTrue(finalCheckResult.Output.Contains("No corrupted"), "Final corruption check failed");
        }

        #endregion

        #region Helper Methods

        private async Task CreateTestFiles()
        {
            var jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };
            // Create various test files
            await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "test1.txt"), "Test file 1 content");
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "test2.bin"), [0x01, 0x02, 0x03, 0x04]);

            // Create subdirectory with file
            var subDir = Path.Combine(_sourceDirectory, "subfolder");
            Directory.CreateDirectory(subDir);
            await File.WriteAllTextAsync(Path.Combine(subDir, "nested.txt"), "Nested file content");

            // Create JSON file
            var jsonContent = new { name = "test", value = 42, items = new[] { "a", "b", "c" } };
            await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "config.json"), JsonSerializer.Serialize(jsonContent, jsonSerializerOptions));
        }

        private async Task<ProcessResult> RunBackupToolAsync(string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _backupToolPath,
                Arguments = arguments,
                WorkingDirectory = _testRootDirectory, // Set working directory for database creation
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) => {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (sender, e) => {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                Output = outputBuilder.ToString(),
                Error = errorBuilder.ToString()
            };
        }

        private static string FindSolutionRoot(string startPath)
        {
            var current = new DirectoryInfo(startPath);
            while (current != null)
            {
                if (current.GetFiles("*.sln").Length > 0)
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            throw new InvalidOperationException("Solution root not found");
        }

        private static void BuildProject(string solutionRoot)
        {
            var buildProcess = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build",
                WorkingDirectory = solutionRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(buildProcess);
            process?.WaitForExit();

            if (process?.ExitCode != 0)
            {
                throw new InvalidOperationException("Failed to build the project");
            }
        }

        private class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
        }

        #endregion
    }
}