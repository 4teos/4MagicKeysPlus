using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

// The factory defaults that are shipped (the defaults folder in the root of the repository): a file that the installer or the
// driver would not take, or a rule the driver would drop, must break the build here, not a keyboard.
public sealed class ShippedDefaultsTests {
    private static string Folder {
        get {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "4MagicKeysPlus.sln"))) {
                directory = directory.Parent;
            }
            return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found"), "defaults");
        }
    }

    public static TheoryData<string> Files() {
        TheoryData<string> files = [];
        foreach (string path in Directory.GetFiles(Folder, "*.json")) {
            files.Add(Path.GetFileName(path));
        }
        return files;
    }

    [Fact]
    public void TheA1843_HasDefaults() {
        Assert.True(File.Exists(Path.Combine(Folder, "A1843.json")));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryFile_IsOfTheModelItIsNamedAfter(string name) {
        FactoryDefaults defaults = FactoryDefaultsFile.Load(Path.Combine(Folder, name));

        Assert.Equal(Path.GetFileNameWithoutExtension(name), defaults.Model.ToString());
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryRule_IsOneTheDriverKeeps(string name) {
        FactoryDefaults defaults = FactoryDefaultsFile.Load(Path.Combine(Folder, name));

        foreach (FactoryRule rule in defaults.Rules) {
            Assert.True(
                DriverConfigParser.ParseRule(rule.Source, rule.Target, rule.FnLockAware) is not null,
                $"{name}: the driver would drop the rule 0x{rule.Source:X8} -> 0x{rule.Target:X8} ({rule.Comment}).");
        }
    }

    [Fact]
    public void TheFilesAreCopiedNextToTheExe() {
        foreach (string path in Directory.GetFiles(Folder, "*.json")) {
            Assert.True(
                File.Exists(Path.Combine(AppContext.BaseDirectory, "defaults", Path.GetFileName(path))),
                $"{Path.GetFileName(path)} is not in the defaults folder of the build output.");
        }
    }
}
