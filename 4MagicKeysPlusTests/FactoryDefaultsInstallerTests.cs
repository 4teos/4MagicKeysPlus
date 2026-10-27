using Microsoft.Win32;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusTests;

[Collection(TestCollections.Registry)]
public sealed class FactoryDefaultsInstallerTests : IDisposable {
    private const string A1843Json = """{ "model": "A1843", "fnLock": true, "rules": [ { "source": "0x68", "target": "0x46" } ] }""";
    private const string A1644Json = """{ "model": "A1644", "rules": [ { "source": "0x69", "target": "0x47" } ] }""";

    private readonly ScratchKey scratch = new();
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"mkp-defaults-{Guid.NewGuid():N}");
    private readonly RegistryFactoryDefaults target;
    private readonly string keyboardsPath;

    public FactoryDefaultsInstallerTests() {
        Directory.CreateDirectory(folder);
        keyboardsPath = scratch.Path("Keyboards");
        target = new RegistryFactoryDefaults(Registry.CurrentUser, scratch.Path("Defaults"), keyboardsPath);
    }

    public void Dispose() {
        Directory.Delete(folder, recursive: true);
        scratch.Dispose();
    }

    private void WriteFile(string name, string json) {
        File.WriteAllText(Path.Combine(folder, name), json);
    }

    [Fact]
    public void EveryFile_BecomesTheDefaultsOfItsModel() {
        WriteFile("A1843.json", A1843Json);
        WriteFile("A1644.json", A1644Json);

        DefaultsInstallResult result = FactoryDefaultsInstaller.Install(folder, target);

        Assert.Empty(result.Problems);
        Assert.Equal([KeyboardProduct.A1644, KeyboardProduct.A1843], result.Installed);
        Assert.True(target.Read(KeyboardProduct.A1843)!.FnLock);
        Assert.Equal(0x69u, Assert.Single(target.Read(KeyboardProduct.A1644)!.Rules).Source);
    }

    [Fact]
    public void InstallingAgain_ReplacesTheDefaults() {
        WriteFile("A1843.json", A1843Json);
        FactoryDefaultsInstaller.Install(folder, target);
        WriteFile("A1843.json", """{ "model": "A1843" }""");

        DefaultsInstallResult result = FactoryDefaultsInstaller.Install(folder, target);

        Assert.Empty(result.Problems);
        FactoryDefaults read = target.Read(KeyboardProduct.A1843)!;
        Assert.False(read.FnLock);
        Assert.Empty(read.Rules);
    }

    [Fact]
    public void WhenOneFileIsBad_NothingIsWritten() {
        WriteFile("A1643.json", """{ "model": "A1643" }""");
        WriteFile("A1644.json", A1644Json);
        WriteFile("A1843.json", A1843Json);

        DefaultsInstallResult result = FactoryDefaultsInstaller.Install(folder, target);

        string problem = Assert.Single(result.Problems);
        Assert.StartsWith("A1643.json", problem);
        Assert.Empty(result.Installed);
        Assert.Null(target.Read(KeyboardProduct.A1644));
        Assert.Null(target.Read(KeyboardProduct.A1843));
    }

    [Fact]
    public void EveryBadFile_IsReported() {
        WriteFile("A1644.json", "not json");
        WriteFile("A1843.json", """{ "model": "A1843", "rules": [ { "source": "x", "target": "0x1" } ] }""");

        DefaultsInstallResult result = FactoryDefaultsInstaller.Install(folder, target);

        Assert.Equal(2, result.Problems.Count);
    }

    [Fact]
    public void AFileOfAnotherModelThanItsName_IsRefused() {
        WriteFile("A1843.json", A1644Json);

        DefaultsInstallResult result = FactoryDefaultsInstaller.Install(folder, target);

        string problem = Assert.Single(result.Problems);
        Assert.Contains("A1644.json", problem);
        Assert.Null(target.Read(KeyboardProduct.A1644));
    }

    [Fact]
    public void OtherFilesThanJsonOnes_AreIgnored() {
        WriteFile("A1843.json", A1843Json);
        WriteFile("notes.txt", "not json at all");

        DefaultsInstallResult result = FactoryDefaultsInstaller.Install(folder, target);

        Assert.Empty(result.Problems);
        Assert.Equal([KeyboardProduct.A1843], result.Installed);
    }

    [Fact]
    public void AFolderWithoutFiles_IsAProblem() {
        DefaultsInstallResult result = FactoryDefaultsInstaller.Install(folder, target);

        Assert.Single(result.Problems);
        Assert.Empty(result.Installed);
    }

    [Fact]
    public void AFolderThatIsNotThere_IsAProblem() {
        DefaultsInstallResult result = FactoryDefaultsInstaller.Install(Path.Combine(folder, "missing"), target);

        Assert.Single(result.Problems);
        Assert.Empty(result.Installed);
    }

    [Fact]
    public void TheProfilesOfTheKeyboards_AreNeverTouched() {
        KeyboardProfile profile = new(Guid.NewGuid(), KeyboardProduct.A1843, [KeyboardIdentity.Parse("bt:C4141104498E")!]);
        new RegistryKeyboardProfileStore(Registry.CurrentUser, keyboardsPath).Save(profile);
        string profilePath = $@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}";
        using (RegistryKey rule = Registry.CurrentUser.CreateSubKey($@"{profilePath}\Rules\00000004", writable: true)) {
            rule.SetValue("Source", 4);
            rule.SetValue("Target", 5);
        }
        WriteFile("A1843.json", A1843Json);

        FactoryDefaultsInstaller.Install(folder, target);

        using RegistryKey key = Registry.CurrentUser.OpenSubKey(profilePath)!;
        Assert.Null(key.GetValue("FnLock"));
        using RegistryKey rules = key.OpenSubKey("Rules")!;
        Assert.Equal(["00000004"], rules.GetSubKeyNames());
    }
}
