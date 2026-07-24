using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests
{
    [TestClass]
    public class PathHelperTests
    {
        private readonly BackupTrayContext _context;

        public PathHelperTests()
        {
            _context = new BackupTrayContext(forTesting: true);
        }

        [TestMethod]
        public void GetRelativePath_ShouldReturnCorrectRelativePath()
        {
            string basePath = @"C:\Users\Test\AppData\Roaming\Mozilla\Firefox\Profiles\abcd.default";
            string fullPath = @"C:\Users\Test\AppData\Roaming\Mozilla\Firefox\Profiles\abcd.default\places.sqlite";

            string result = _context.GetRelativePath(basePath, fullPath);

            Assert.AreEqual("places.sqlite", result);
        }

        [TestMethod]
        public void GetRelativePath_ShouldHandleSubfolders()
        {
            string basePath = @"C:\Users\Test\AppData\Roaming\Mozilla\Firefox\Profiles\abcd.default";
            string fullPath = @"C:\Users\Test\AppData\Roaming\Mozilla\Firefox\Profiles\abcd.default\extensions\adblock.xpi";

            string result = _context.GetRelativePath(basePath, fullPath);

            Assert.AreEqual("extensions\\adblock.xpi", result);
        }

        [TestMethod]
        public void GetRelativePath_ShouldHandleDifferentDriveLetters()
        {
            string basePath = @"C:\Users\Test\Profile";
            string fullPath = @"D:\Other\file.txt";

            string result = _context.GetRelativePath(basePath, fullPath);

            Assert.AreEqual("D:\\Other\\file.txt", result);
        }
    }
}