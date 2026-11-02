using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

// The default settings that are shipped (the defaults folder in the root of the repository): a file that the installer or the
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
    public void EveryModel_HasDefaults() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            Assert.True(File.Exists(Path.Combine(Folder, $"{product}.json")), $"{product} has no defaults.");
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryFile_IsOfTheModelItIsNamedAfter(string name) {
        DefaultSettings defaults = DefaultSettingsFile.Load(Path.Combine(Folder, name));

        Assert.Equal(Path.GetFileNameWithoutExtension(name), defaults.Model.ToString());
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryRule_IsOneTheDriverKeeps(string name) {
        DefaultSettings defaults = DefaultSettingsFile.Load(Path.Combine(Folder, name));

        foreach (DefaultRule rule in defaults.Rules) {
            Assert.True(
                DriverConfigParser.ParseRule(rule.Source, rule.Target, rule.FnLockAware) is not null,
                $"{name}: the driver would drop the rule 0x{rule.Source:X8} -> 0x{rule.Target:X8} ({rule.Comment}).");
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryRule_StartsFromAKeyTheKeyboardHas(string name) {
        DefaultSettings defaults = DefaultSettingsFile.Load(Path.Combine(Folder, name));
        HashSet<PhysicalKey> keys = [.. Enum.GetValues<LayoutStandard>().SelectMany(standard => KeyboardLayout.GetKeys(KeyboardModel.From(defaults.Model), standard)).SelectMany(row => row.RowKeys).Select(key => key.Kind)];
        bool touchId = keys.Contains(PhysicalKey.TouchId);

        foreach (DefaultRule rule in defaults.Rules) {
            KeyRule parsed = DriverConfigParser.ParseRule(rule.Source, rule.Target, rule.FnLockAware, touchId)!;

            Assert.True(parsed.Source.IsSubsetOf(keys), $"{name}: the rule '{rule.Comment}' starts from a key that {defaults.Model} does not have.");
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
