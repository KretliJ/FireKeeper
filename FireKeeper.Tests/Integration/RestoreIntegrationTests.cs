// FireKeeper.Tests/Integration/RestoreIntegrationTests.cs
using System;
using System.IO;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests.Integration
{
    [TestClass]
    [TestCategory("Integration")]
    public class RestoreIntegrationTests
    {
        private string _testProfilePath;
        private string _testBackupPath;
        private string _zipPath;
        private BackupTrayContext _context;

        [TestInitialize]
        public void Setup()
        {
            _testProfilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _testBackupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(_testProfilePath);
            Directory.CreateDirectory(_testBackupPath);

            // Criar perfil de teste
            File.WriteAllText(Path.Combine(_testProfilePath, "prefs.js"), "user_pref('test', true);");
            File.WriteAllText(Path.Combine(_testProfilePath, "places.sqlite"), "test history data");
            
            var extensionsDir = Path.Combine(_testProfilePath, "extensions");
            Directory.CreateDirectory(extensionsDir);
            File.WriteAllText(Path.Combine(extensionsDir, "test.xpi"), "extension data");

            _context = new BackupTrayContext(forTesting: true);
            _context.SetTestPaths(_testProfilePath, _testBackupPath);

            // Criar backup
            _zipPath = Path.Combine(_testBackupPath, "backup.zip");
            _context.CreateBackupZip(_testProfilePath, _zipPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_testProfilePath, true); } catch { }
            try { Directory.Delete(_testBackupPath, true); } catch { }
        }

        [TestMethod]
        public void RestoreBackup_ShouldRestoreAllFiles()
        {
            // Arrange
            var tempExtractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractDir);

            try
            {
                // Act
                ZipFile.ExtractToDirectory(_zipPath, tempExtractDir);

                // Assert
                Assert.IsTrue(File.Exists(Path.Combine(tempExtractDir, "prefs.js")));
                Assert.IsTrue(File.Exists(Path.Combine(tempExtractDir, "places.sqlite")));
                Assert.IsTrue(File.Exists(Path.Combine(tempExtractDir, "extensions/test.xpi")));
            }
            finally
            {
                if (Directory.Exists(tempExtractDir))
                    Directory.Delete(tempExtractDir, true);
            }
        }

        [TestMethod]
        public void ClearProfileDirectory_ShouldDeleteAllFiles()
        {
            // Arrange
            Assert.IsTrue(Directory.GetFiles(_testProfilePath, "*", SearchOption.AllDirectories).Length > 0);

            // Act
            _context.ClearProfileDirectory(_testProfilePath);

            // Assert
            Assert.AreEqual(0, Directory.GetFiles(_testProfilePath, "*", SearchOption.AllDirectories).Length);
        }
    }
}