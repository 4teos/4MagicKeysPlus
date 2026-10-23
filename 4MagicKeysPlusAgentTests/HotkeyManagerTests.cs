using _4MagicKeysPlus.Core;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

[Collection(StringsCollection.Name)]
public class HotkeyManagerTests {
    private const uint VkF16 = 0x7F;
    private const uint VkF17 = 0x80;

    private sealed class FakeRegistrar : IHotkeyRegistrar {
        public List<string> Calls { get; } = [];
        public HashSet<string> Registered { get; } = [];
        public Dictionary<string, int> Failing { get; } = [];

        public bool RegisterHotKey(HotkeyEntry entry, out int win32Error) {
            Calls.Add($"register {entry.RegistryName}");
            if (Failing.TryGetValue(entry.RegistryName, out win32Error)) {
                return false;
            }
            Registered.Add(entry.RegistryName);
            return true;
        }

        public void UnregisterAll() {
            Calls.Add("unregister");
            Registered.Clear();
        }
    }

    private static HotkeyEntry Entry(int id, string name, uint vk = VkF16, uint modifiers = 0, string path = @"C:\app.exe") {
        return new HotkeyEntry(id, name, name, vk, modifiers, path);
    }

    [Fact]
    public void Reload_RegistersEveryEntryAndReportsSuccess() {
        FakeRegistrar registrar = new();
        HotkeyManager manager = new(registrar, () => [Entry(1, "A"), Entry(2, "B", VkF17)]);

        IReadOnlyList<HotkeyRegistration> results = manager.Reload();

        Assert.Equal(["A", "B"], registrar.Registered.Order());
        Assert.All(results, r => Assert.True(r.Succeeded));
        Assert.Equal(["A", "B"], results.Select(r => r.RegistryName));
    }

    [Fact]
    public void Reload_AFailedRegistrationDoesNotStopTheOthers() {
        FakeRegistrar registrar = new();
        registrar.Failing["A"] = 1409;
        HotkeyManager manager = new(registrar, () => [Entry(1, "A"), Entry(2, "B", VkF17)]);

        IReadOnlyList<HotkeyRegistration> results = manager.Reload();

        Assert.Equal(["B"], registrar.Registered);
        Assert.Equal(1409, results.Single(r => r.RegistryName == "A").Win32Error);
        Assert.True(results.Single(r => r.RegistryName == "B").Succeeded);
    }

    [Fact]
    public void Reload_ReleasesTheOldHotkeysBeforeRegisteringAndReplacesThePaths() {
        FakeRegistrar registrar = new();
        List<HotkeyEntry> config = [Entry(1, "A", path: @"C:\old.exe")];
        HotkeyManager manager = new(registrar, () => config);
        manager.Reload();

        config = [Entry(1, "B", path: @"C:\new.exe")];
        registrar.Calls.Clear();
        manager.Reload();

        Assert.Equal(["unregister", "register B"], registrar.Calls);
        Assert.Equal(@"C:\new.exe", manager.FindPath(1));
        Assert.Null(manager.FindPath(2));
    }

    [Fact]
    public void Pause_ReleasesEverythingAndResumeTakesItBackWithoutRereadingTheConfiguration() {
        FakeRegistrar registrar = new();
        int loads = 0;
        HotkeyManager manager = new(registrar, () => {
            loads++;
            return [Entry(1, "A")];
        });
        manager.Reload();

        manager.Pause();
        Assert.Empty(registrar.Registered);

        IReadOnlyList<HotkeyRegistration> results = manager.Resume();

        Assert.Equal(["A"], registrar.Registered);
        Assert.True(results.Single().Succeeded);
        Assert.Equal(1, loads);
    }

    [Fact]
    public void Reload_WhilePaused_RegistersAgain() {
        FakeRegistrar registrar = new();
        HotkeyManager manager = new(registrar, () => [Entry(1, "A")]);
        manager.Reload();
        manager.Pause();

        manager.Reload();

        Assert.Equal(["A"], registrar.Registered);
    }

    [Fact]
    public void DescribeFailures_NamesTheEntryItsCombinationAndTheReason() {
        FakeRegistrar registrar = new();
        registrar.Failing["Calc"] = 1409;
        HotkeyManager manager = new(registrar, () => [Entry(1, "Calc", VkF16, modifiers: 0x0002 | 0x0001)]);

        IReadOnlyList<string> lines = manager.DescribeFailures(manager.Reload());

        string line = Assert.Single(lines);
        Assert.StartsWith("Calc (Ctrl+Alt+F16): ", line);
        Assert.True(line.Length > "Calc (Ctrl+Alt+F16): ".Length);
    }

    [Theory]
    [InlineData("ru", "Calc (Ctrl+Alt+F16): Это сочетание уже занято другой программой.")]
    [InlineData("en", "Calc (Ctrl+Alt+F16): The combination is already used by another program.")]
    public void DescribeFailures_SaysInTheChosenLanguage_ThatTheCombinationIsTaken(string language, string expected) {
        using LanguageScope scope = new(language);
        FakeRegistrar registrar = new();
        registrar.Failing["Calc"] = 1409;
        HotkeyManager manager = new(registrar, () => [Entry(1, "Calc", VkF16, modifiers: 0x0002 | 0x0001)]);

        IReadOnlyList<string> lines = manager.DescribeFailures(manager.Reload());

        Assert.Equal(expected, Assert.Single(lines));
    }

    [Fact]
    public void DescribeFailures_IsEmptyWhenEverythingRegistered() {
        HotkeyManager manager = new(new FakeRegistrar(), () => [Entry(1, "A")]);

        Assert.Empty(manager.DescribeFailures(manager.Reload()));
    }
}
