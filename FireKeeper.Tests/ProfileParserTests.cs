using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests
{
    [TestClass]
    public class ProfileParserTests
    {
        private readonly BackupTrayContext _context;

        public ProfileParserTests()
        {
            _context = new BackupTrayContext(forTesting: true);
        }

        [TestMethod]
        public void ParseProfilesIni_ShouldReturnDefaultProfilePath()
        {
            string iniContent = @"
        [Install4F96D1932A9F858E]
        Default=Profiles/abcd.default
        Locked=1

        [Profile0]
        Name=default
        IsRelative=1
        Path=Profiles/abcd.default
        Default=1
        ";
            string iniPath = Path.Combine(Path.GetTempPath(), "test_profiles.ini");
            File.WriteAllText(iniPath, iniContent);

            string profilesPath = Path.GetTempPath();
            
            // NOVO: Criar a estrutura de diretórios falsa no Temp 
            // para satisfazer o Directory.Exists() do FireKeeper.cs
            string fakeProfileDir = Path.Combine(profilesPath, "Profiles", "abcd.default");
            Directory.CreateDirectory(fakeProfileDir);

            try
            {
                string result = _context.ParseProfilesIni(iniPath, profilesPath);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Contains("abcd.default"));
            }
            finally
            {
                // Limpeza do arquivo
                if (File.Exists(iniPath))
                    File.Delete(iniPath);
                    
                // NOVO: Limpeza do diretório falso
                if (Directory.Exists(Path.Combine(profilesPath, "Profiles")))
                    Directory.Delete(Path.Combine(profilesPath, "Profiles"), true);
            }
        }

        [TestMethod]
        public void ParseProfilesIni_ShouldReturnNull_WhenNoDefaultFound()
        {
            string iniContent = @"
[Profile0]
Name=default
IsRelative=1
Path=Profiles/abcd.default
";
            string iniPath = Path.Combine(Path.GetTempPath(), "test_profiles.ini");
            File.WriteAllText(iniPath, iniContent);

            string profilesPath = Path.GetTempPath();

            try
            {
                string result = _context.ParseProfilesIni(iniPath, profilesPath);

                Assert.IsNull(result);
            }
            finally
            {
                if (File.Exists(iniPath))
                    File.Delete(iniPath);
            }
        }
    }
}