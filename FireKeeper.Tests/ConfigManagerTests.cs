// FireKeeper.Tests/ConfigManagerTests.cs
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests
{
    [TestClass]
    public class ConfigManagerTests
    {
        [TestMethod]
        public void GetAppSettings_ShouldReturnSettings_WhenConfigExists()
        {
            // Arrange - create test config file
            string testConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FireKeeper",
                "appsettings.json");
            
            // If exists, use. Else, launch exception
            try
            {
                // Act
                var settings = ConfigManager.GetAppSettings();

                // Assert
                Assert.IsNotNull(settings);
            }
            catch
            {
                Assert.Fail("GetAppSettings threw an exception");
            }
        }

        [TestMethod]
        public void GetAppSettings_ShouldNotThrowException_WhenCalled()
        {
            // Arrange & Act & Assert
            try
            {
                var settings = ConfigManager.GetAppSettings();
                Assert.IsTrue(true); // Passed
            }
            catch (Exception ex)
            {
                Assert.Fail($"GetAppSettings threw an exception: {ex.Message}");
            }
        }

        [TestMethod]
        public void SaveUserConfig_ShouldSaveSettingsToAppData()
        {
            // Arrange
            var settings = new AppSettings
            {
                DebugEnabled = true
            };

            // Act
            ConfigManager.SaveUserConfig(settings);

            // Assert
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FireKeeper",
                "appsettings.json");
            
            Assert.IsTrue(File.Exists(appDataPath));
        }

        [TestMethod]
        public void SaveUserConfig_ShouldPersistDebugEnabledSetting()
        {
            // Arrange
            var originalSettings = new AppSettings
            {
                DebugEnabled = true
            };
            ConfigManager.SaveUserConfig(originalSettings);

            // Act
            var loadedSettings = ConfigManager.GetAppSettings();

            // Assert
            Assert.IsNotNull(loadedSettings);
            Assert.IsTrue(loadedSettings.DebugEnabled);
        }
    }
}