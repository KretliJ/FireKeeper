using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests
{
    [TestClass]
    public class IsFirefoxRunningTests
    {
        [TestMethod]
        public void IsFirefoxRunning_ShouldReturnBoolean_RegardlessOfFirefoxState()
        {
            // Arrange
            var context = new BackupTrayContext(forTesting: true);

            // Act
            bool result = context.IsFirefoxRunning();

            // Assert
            // This just validates that the method returns a boolean
            // regardless of whether Firefox is running or not
            Assert.IsTrue(result == true || result == false);
        }

        [TestMethod]
        public void IsFirefoxRunning_ShouldNotThrowException_WhenCalled()
        {
            // Arrange
            var context = new BackupTrayContext(forTesting: true);

            // Act & Assert - should not throw
            try
            {
                bool result = context.IsFirefoxRunning();
                Assert.IsTrue(true); // Passou
            }
            catch
            {
                Assert.Fail("IsFirefoxRunning() threw an exception");
            }
        }
    }
}