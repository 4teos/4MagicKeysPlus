using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

[Collection(TestCollections.Registry)]
public sealed class DefaultsCommandTests : IDisposable {
    private readonly ScratchKey scratch = new();
    private readonly string appFolder = Path.Combine(Path.GetTempPath(), $"mkp-app-{Guid.NewGuid():N}");
    private readonly RegistryFactoryDefaults target;

    public DefaultsCommandTests() {
        Directory.CreateDirectory(Path.Combine(appFolder, "defaults"));
        target = new RegistryFactoryDefaults(Registry.CurrentUser, scratch.Path("Defaults"), scratch.Path("Keyboards"));
    }

    public void Dispose() {
        Directory.Delete(appFolder, recursive: true);
        scratch.Dispose();
    }

    private void WriteDefault(string folder, string name, string json) {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), json);
    }

    [Theory]
    [InlineData("--install-defaults")]
    [InlineData("--INSTALL-DEFAULTS")]
    public void TheSwitch_AsksForTheCommand(string argument) {
        Assert.True(DefaultsCommand.Asks([argument]));
        Assert.True(DefaultsCommand.Asks([argument, @"C:\somewhere"]));
    }

    [Fact]
    public void WithoutArguments_NothingIsAskedFor() {
        Assert.False(DefaultsCommand.Asks([]));
    }

    [Fact]
    public void OnlyTheFirstArgument_CanAskForTheCommand() {
        Assert.False(DefaultsCommand.Asks(["--other"]));
        Assert.False(DefaultsCommand.Asks(["a", "--install-defaults"]));
    }

    [Fact]
    public void WithoutAFolder_TheDefaultsFolderNextToTheExeIsInstalled() {
        WriteDefault(Path.Combine(appFolder, "defaults"), "A1843.json", """{ "model": "A1843", "fnLock": true }""");
        StringWriter output = new();

        int exitCode = DefaultsCommand.Run(["--install-defaults"], appFolder, target, output);

        Assert.Equal(0, exitCode);
        Assert.True(target.Read(KeyboardProduct.A1843)!.FnLock);
        Assert.Contains("A1843", output.ToString());
    }

    [Fact]
    public void AGivenFolder_IsInstalledInstead() {
        string other = Path.Combine(appFolder, "elsewhere");
        WriteDefault(other, "A1644.json", """{ "model": "A1644" }""");
        StringWriter output = new();

        int exitCode = DefaultsCommand.Run(["--install-defaults", other], appFolder, target, output);

        Assert.Equal(0, exitCode);
        Assert.NotNull(target.Read(KeyboardProduct.A1644));
    }

    [Fact]
    public void AProblem_IsWrittenOutAndTheExitCodeIsOne() {
        WriteDefault(Path.Combine(appFolder, "defaults"), "A1843.json", """{ "model": "nope" }""");
        StringWriter output = new();

        int exitCode = DefaultsCommand.Run(["--install-defaults"], appFolder, target, output);

        Assert.Equal(1, exitCode);
        Assert.Contains("A1843.json", output.ToString());
        Assert.Null(target.Read(KeyboardProduct.A1843));
    }
}
