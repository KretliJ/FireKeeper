using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests
{
    [TestClass]
    public class BackupRulesTests
    {
        private readonly BackupTrayContext _context;

        public BackupRulesTests()
        {
            _context = new BackupTrayContext(forTesting: true);
        }

        [TestMethod]
        public void ShouldSkipFile_ShouldReturnTrue_ForLockFiles()
        {
            string relPath = "places.sqlite.lock";
            var excludeFolders = new HashSet<string>();
            var excludeExtensions = new HashSet<string>();

            bool result = _context.ShouldSkipFile(relPath, excludeFolders, excludeExtensions);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldSkipFile_ShouldReturnTrue_ForExcludedExtensions()
        {
            string relPath = "file.tmp";
            var excludeFolders = new HashSet<string>();
            var excludeExtensions = new HashSet<string> { ".tmp", ".log" };

            bool result = _context.ShouldSkipFile(relPath, excludeFolders, excludeExtensions);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldSkipFile_ShouldReturnTrue_ForExcludedFolders()
        {
            string relPath = "cache2\\file.txt";
            var excludeFolders = new HashSet<string> { "cache2" };
            var excludeExtensions = new HashSet<string>();

            bool result = _context.ShouldSkipFile(relPath, excludeFolders, excludeExtensions);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldSkipFile_ShouldReturnFalse_ForNormalFile()
        {
            string relPath = "places.sqlite";
            var excludeFolders = new HashSet<string>();
            var excludeExtensions = new HashSet<string>();

            bool result = _context.ShouldSkipFile(relPath, excludeFolders, excludeExtensions);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldIncludeFile_ShouldReturnTrue_ForRootImportantFiles()
        {
            string relPath = "prefs.js";
            var includeFolders = new HashSet<string>();

            bool result = _context.ShouldIncludeFile(relPath, includeFolders);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldIncludeFile_ShouldReturnTrue_ForIncludedFolder()
        {
            string relPath = "extensions\\adblock.xpi";
            var includeFolders = new HashSet<string> { "extensions" };

            bool result = _context.ShouldIncludeFile(relPath, includeFolders);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldIncludeFile_ShouldReturnFalse_ForNonIncludedFile()
        {
            string relPath = "cache2\\file.txt";
            var includeFolders = new HashSet<string> { "extensions" };

            bool result = _context.ShouldIncludeFile(relPath, includeFolders);

            Assert.IsFalse(result);
        }
    }
}