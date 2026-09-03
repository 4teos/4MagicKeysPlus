using Moq;
using _4MagicKeysPlusAppLauncher;

namespace _4MagicKeysPlusAppLauncherTests;

public class AppLaunchConfigTests {
    private const uint VkF16 = 0x7F;

    private static Mock<IRegistryKeyReader> CreateAppKeyMock(string? path, object? key, object? modifier = null) {
        Mock<IRegistryKeyReader> appKey = new Mock<IRegistryKeyReader>();
        appKey.Setup(k => k.GetValue("Path")).Returns(path);
        appKey.Setup(k => k.GetValue("Key")).Returns(key);
        appKey.Setup(k => k.GetValue("Modifier")).Returns(modifier);
        return appKey;
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenRootKeyIsNull() {
        List<AppLaunchEntry> entries = AppLaunchConfig.Load(null);

        Assert.Empty(entries);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenThereAreNoSubKeys() {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns([]);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        Assert.Empty(entries);
    }

    [Fact]
    public void Load_ReturnsEntry_ForValidSubKey() {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["Notepad"]);
        Mock<IRegistryKeyReader> appKey = CreateAppKeyMock(@"C:\Windows\System32\notepad.exe", (int)VkF16);
        root.Setup(r => r.OpenSubKey("Notepad")).Returns(appKey.Object);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        AppLaunchEntry entry = Assert.Single(entries);
        Assert.Equal("Notepad", entry.Name);
        Assert.Equal(VkF16, entry.VirtualKey);
        Assert.Equal(0u, entry.Modifiers);
        Assert.Equal(@"C:\Windows\System32\notepad.exe", entry.ExecutablePath);
        Assert.Equal(1, entry.HotkeyId);
    }

    [Fact]
    public void Load_Skips_WhenOpenSubKeyReturnsNull() {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["Ghost"]);
        root.Setup(r => r.OpenSubKey("Ghost")).Returns((IRegistryKeyReader?)null);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        Assert.Empty(entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_Skips_WhenPathIsMissingOrBlank(string? path) {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["Broken"]);
        Mock<IRegistryKeyReader> appKey = CreateAppKeyMock(path, (int)VkF16);
        root.Setup(r => r.OpenSubKey("Broken")).Returns(appKey.Object);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        Assert.Empty(entries);
    }

    [Fact]
    public void Load_Skips_WhenKeyIsMissing() {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["Broken"]);
        Mock<IRegistryKeyReader> appKey = CreateAppKeyMock(@"C:\app.exe", key: null);
        root.Setup(r => r.OpenSubKey("Broken")).Returns(appKey.Object);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        Assert.Empty(entries);
    }

    [Fact]
    public void Load_Skips_WhenKeyIsNotAnInt() {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["Broken"]);
        Mock<IRegistryKeyReader> appKey = CreateAppKeyMock(@"C:\app.exe", key: "not a dword");
        root.Setup(r => r.OpenSubKey("Broken")).Returns(appKey.Object);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        Assert.Empty(entries);
    }

    [Fact]
    public void Load_DefaultsModifierToZero_WhenModifierIsMissing() {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["Notepad"]);
        Mock<IRegistryKeyReader> appKey = CreateAppKeyMock(@"C:\app.exe", (int)VkF16, modifier: null);
        root.Setup(r => r.OpenSubKey("Notepad")).Returns(appKey.Object);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        Assert.Equal(0u, Assert.Single(entries).Modifiers);
    }

    [Fact]
    public void Load_MasksOutUnrecognizedModifierBits() {
        // 0x1F = MOD_ALT|MOD_CONTROL|MOD_SHIFT|MOD_WIN (0x0F) plus an unknown bit (0x10).
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["Notepad"]);
        Mock<IRegistryKeyReader> appKey = CreateAppKeyMock(@"C:\app.exe", (int)VkF16, modifier: 0x1F);
        root.Setup(r => r.OpenSubKey("Notepad")).Returns(appKey.Object);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        Assert.Equal(0x0Fu, Assert.Single(entries).Modifiers);
    }

    [Fact]
    public void Load_AssignsSequentialHotkeyIds_InSubKeyEnumerationOrder() {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["AppA", "AppB"]);
        root.Setup(r => r.OpenSubKey("AppA")).Returns(CreateAppKeyMock(@"C:\a.exe", (int)VkF16).Object);
        root.Setup(r => r.OpenSubKey("AppB")).Returns(CreateAppKeyMock(@"C:\b.exe", (int)VkF16).Object);

        List<AppLaunchEntry> entries = AppLaunchConfig.Load(root.Object);

        Assert.Equal(2, entries.Count);
        Assert.Equal(1, entries[0].HotkeyId);
        Assert.Equal(2, entries[1].HotkeyId);
    }

    [Fact]
    public void Load_DisposesEachAppSubKey() {
        Mock<IRegistryKeyReader> root = new Mock<IRegistryKeyReader>();
        root.Setup(r => r.GetSubKeyNames()).Returns(["Notepad"]);
        Mock<IRegistryKeyReader> appKey = CreateAppKeyMock(@"C:\app.exe", (int)VkF16);
        root.Setup(r => r.OpenSubKey("Notepad")).Returns(appKey.Object);

        AppLaunchConfig.Load(root.Object);

        appKey.Verify(k => k.Dispose(), Times.Once);
    }
}
