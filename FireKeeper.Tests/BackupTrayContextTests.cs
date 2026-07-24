using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests
{
    [TestClass]
    public class BackupTrayContextTests
    {
        [TestMethod]
        public void Constructor_ShouldInitializeConfig_WhenCalled()
        {
            // Arrange & Act
            var context = new BackupTrayContext(forTesting: true);

            // Assert
            Assert.IsNotNull(context);
        }

        [TestMethod]
        public void GetSyncFolder_ShouldReturnDefaultFolder_WhenConfigIsEmpty()
        {
            // Arrange
            var context = new BackupTrayContext(forTesting: true);

            // Act
            string syncFolder = context.GetSyncFolder();

            // Assert
            Assert.IsNotNull(syncFolder);
            Assert.IsTrue(!string.IsNullOrEmpty(syncFolder));
        }

        [TestMethod]
        public void ShouldShowNotifications_ShouldReturnTrue_WhenManagerIsNull()
        {
            // Arrange
            var context = new BackupTrayContext(forTesting: true);

            // Act
            bool result = context.ShouldShowNotifications();

            // Assert
            Assert.IsTrue(result);
        }
    }
}