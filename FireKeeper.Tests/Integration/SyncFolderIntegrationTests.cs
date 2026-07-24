// FireKeeper.Tests/Integration/SyncFolderIntegrationTests.cs
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests.Integration
{
    [TestClass]
    [TestCategory("Integration")]
    public class SyncFolderIntegrationTests
    {
        private string _testConfigPath;
        private string _testSyncFolder;

        [TestInitialize]
        public void Setup()
        {
            _testSyncFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testSyncFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testSyncFolder))
                Directory.Delete(_testSyncFolder, true);
        }

        [TestMethod]
        public void GetSyncFolder_ShouldReturnDefaultDesktopFolder()
        {
            // Arrange
            var context = new BackupTrayContext(forTesting: true);

            // Act
            string syncFolder = context.GetSyncFolder();

            // Assert
            Assert.IsNotNull(syncFolder);
            Assert.IsTrue(Directory.Exists(syncFolder));
        }

        [TestMethod]
        public void GetSyncFolder_ShouldFallbackToDesktop_WhenConfiguredFolderInvalid()
        {
            // Arrange
            var context = new BackupTrayContext(forTesting: true);
            string invalidPath = @"Z:\Invalid\Folder\That\Does\Not\Exist";
            
            // Simular config inválida
            context.SetSyncFolder(invalidPath);

            // Act
            string syncFolder = context.GetSyncFolder();

            // Assert
            Assert.IsNotNull(syncFolder);
            Assert.IsTrue(Directory.Exists(syncFolder));
            Assert.AreNotEqual(invalidPath, syncFolder); // Fallback para Desktop
        }
    }
}