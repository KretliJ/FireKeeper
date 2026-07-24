// FireKeeper.Tests/Integration/BackupIntegrationTests.cs
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests.Integration
{
    [TestClass]
    [TestCategory("Integration")]
    public class BackupIntegrationTests
    {
        private string _testProfilePath;
        private string _testBackupPath;
        private BackupTrayContext _context;

        [TestInitialize]
        public void Setup()
        {
            // Criar pastas temporárias para teste
            _testProfilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _testBackupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(_testProfilePath);
            Directory.CreateDirectory(_testBackupPath);

            // Criar perfil de teste
            CreateTestProfile();

            // Configurar contexto
            _context = new BackupTrayContext(forTesting: true);
            _context.SetTestPaths(_testProfilePath, _testBackupPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_testProfilePath, true); } catch { }
            try { Directory.Delete(_testBackupPath, true); } catch { }
        }

        private void CreateTestProfile()
        {
            // Criar arquivos importantes
            File.WriteAllText(Path.Combine(_testProfilePath, "prefs.js"), "user_pref('test', true);");
            File.WriteAllText(Path.Combine(_testProfilePath, "places.sqlite"), "test history data");
            File.WriteAllText(Path.Combine(_testProfilePath, "logins.json"), "{\"test\": \"encrypted\"}");
            File.WriteAllText(Path.Combine(_testProfilePath, "cookies.sqlite"), "cookie data");
            File.WriteAllText(Path.Combine(_testProfilePath, "handlers.json"), "{}");
            
            // Criar subpastas
            var extensionsDir = Path.Combine(_testProfilePath, "extensions");
            Directory.CreateDirectory(extensionsDir);
            File.WriteAllText(Path.Combine(extensionsDir, "test.xpi"), "extension data");

            var storageDir = Path.Combine(_testProfilePath, "storage");
            Directory.CreateDirectory(storageDir);
            File.WriteAllText(Path.Combine(storageDir, "data.db"), "storage data");

            // Arquivo de cache (deve ser ignorado)
            var cacheDir = Path.Combine(_testProfilePath, "cache2");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "cachefile"), "cache data");

            // Arquivo de lock (deve ser ignorado)
            File.WriteAllText(Path.Combine(_testProfilePath, "places.sqlite.lock"), "lock data");
        }

        [TestMethod]
        public void CreateBackupZip_ShouldCreateZipFile()
        {
            // Arrange
            string zipPath = Path.Combine(_testBackupPath, "test_backup.zip");

            // Act
            _context.CreateBackupZip(_testProfilePath, zipPath);

            // Assert
            Assert.IsTrue(File.Exists(zipPath));
            Assert.IsTrue(new FileInfo(zipPath).Length > 0);
        }

        [TestMethod]
        public void CreateBackupZip_ShouldIncludeImportantFiles()
        {
            // Arrange
            string zipPath = Path.Combine(_testBackupPath, "test_backup.zip");

            // Act
            _context.CreateBackupZip(_testProfilePath, zipPath);

            // Assert
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var entries = archive.Entries.Select(e => e.FullName).ToList();
                
                // Verificar arquivos importantes
                Assert.IsTrue(entries.Any(e => e.Contains("prefs.js")), "prefs.js should be included");
                Assert.IsTrue(entries.Any(e => e.Contains("places.sqlite")), "places.sqlite should be included");
                Assert.IsTrue(entries.Any(e => e.Contains("logins.json")), "logins.json should be included");
                Assert.IsTrue(entries.Any(e => e.Contains("cookies.sqlite")), "cookies.sqlite should be included");
                Assert.IsTrue(entries.Any(e => e.Contains("extensions/test.xpi")), "extensions should be included");
                Assert.IsTrue(entries.Any(e => e.Contains("storage/data.db")), "storage should be included");
            }
        }

        [TestMethod]
        public void CreateBackupZip_ShouldSkipCacheAndLockFiles()
        {
            // Arrange
            string zipPath = Path.Combine(_testBackupPath, "test_backup.zip");

            // Act
            _context.CreateBackupZip(_testProfilePath, zipPath);

            // Assert
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var entries = archive.Entries.Select(e => e.FullName).ToList();
                
                // Verificar que arquivos de cache e lock NÃO estão incluídos
                Assert.IsFalse(entries.Any(e => e.Contains("cache2")), "cache2 should be excluded");
                Assert.IsFalse(entries.Any(e => e.EndsWith(".lock")), ".lock files should be excluded");
            }
        }

        [TestMethod]
        public void CreateBackupZip_ShouldCreateValidZipFile()
        {
            // Arrange
            string zipPath = Path.Combine(_testBackupPath, "test_backup.zip");

            // Act
            _context.CreateBackupZip(_testProfilePath, zipPath);

            // Assert
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                Assert.IsNotNull(archive);
                Assert.IsTrue(archive.Entries.Count > 0);
            }
        }
    }
}