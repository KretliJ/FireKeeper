using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests
{
    [TestClass]
    public class CleanOldBackupsTests
    {
        private string _testBackupDir;

        [TestInitialize]
        public void Setup()
        {
            _testBackupDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testBackupDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testBackupDir))
                Directory.Delete(_testBackupDir, true);
        }

        [TestMethod]
        public void CleanOldBackups_ShouldDeleteOldBackups_WhenExceedingMaxCount()
        {
            // Arrange
            var context = new BackupTrayContext(forTesting: true);
            context.SetMaxBackups(2);

            // Create 4 test backup files
            for (int i = 0; i < 4; i++)
            {
                string filePath = Path.Combine(_testBackupDir, $"firekeeper_backup_{i:D8}_{i:D6}.zip");
                File.WriteAllText(filePath, "test data");
            }

            // Act
            context.CleanDirectory(_testBackupDir, 2);

            // Assert
            var remainingFiles = Directory.GetFiles(_testBackupDir, "firekeeper_backup_*.zip");
            Assert.AreEqual(2, remainingFiles.Length);
        }

        [TestMethod]
        public void CleanOldBackups_ShouldKeepAllBackups_WhenLessThanMax()
        {
            // Arrange
            var context = new BackupTrayContext(forTesting: true);
            context.SetMaxBackups(5);

            // Create 3 test backup files
            for (int i = 0; i < 3; i++)
            {
                string filePath = Path.Combine(_testBackupDir, $"firekeeper_backup_{i:D8}_{i:D6}.zip");
                File.WriteAllText(filePath, "test data");
            }

            // Act
            context.CleanDirectory(_testBackupDir, 5);

            // Assert
            var remainingFiles = Directory.GetFiles(_testBackupDir, "firekeeper_backup_*.zip");
            Assert.AreEqual(3, remainingFiles.Length);
        }
    }
}