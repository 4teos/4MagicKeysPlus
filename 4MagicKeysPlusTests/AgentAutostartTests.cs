using Microsoft.Win32;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

[Collection(TestCollections.Registry)]
public sealed class AgentAutostartTests : IDisposable {
    private const string Name = RegistryAgentAutostart.ValueName;
    private readonly ScratchKey scratch = new();
    private readonly string agentPath = Path.Combine(Path.GetTempPath(), $"4MagicKeysPlusTests-{Guid.NewGuid():N}.exe");
    private readonly RegistryAgentAutostart autostart;

    public AgentAutostartTests() {
        File.WriteAllBytes(agentPath, []);
        autostart = new RegistryAgentAutostart(Registry.CurrentUser, scratch.Path("Run"), scratch.Path("Approved"), agentPath);
    }

    public void Dispose() {
        scratch.Dispose();
        File.Delete(agentPath);
    }

    private object? RunValue() {
        using RegistryKey? run = scratch.Open("Run");
        return run?.GetValue(Name);
    }

    private void Approve(params byte[] flags) {
        using RegistryKey approved = scratch.Create("Approved");
        approved.SetValue(Name, flags, RegistryValueKind.Binary);
    }

    private byte[]? ApprovedValue() {
        using RegistryKey? approved = scratch.Open("Approved");
        return approved?.GetValue(Name) as byte[];
    }

    [Fact]
    public void WithoutTheRunValue_ItIsOff() {
        Assert.False(autostart.IsEnabled());
    }

    [Fact]
    public void Enabling_WritesTheQuotedPathOfTheAgent() {
        Assert.Null(autostart.Enable());

        Assert.Equal($"\"{agentPath}\"", RunValue());
        Assert.True(autostart.IsEnabled());
    }

    [Fact]
    public void WithoutTheAgent_ItCannotBeEnabled() {
        File.Delete(agentPath);

        string? problem = autostart.Enable();

        Assert.NotNull(problem);
        Assert.Contains(agentPath, problem);
        Assert.Null(RunValue());
        Assert.False(autostart.IsEnabled());
    }

    [Fact]
    public void Disabling_KeepsTheValue_AndSwitchesItOffLikeTaskManagerDoes() {
        autostart.Enable();

        autostart.Disable();

        Assert.Equal($"\"{agentPath}\"", RunValue());
        Assert.Equal(0x03, ApprovedValue()![0]);
        Assert.False(autostart.IsEnabled());
    }

    [Fact]
    public void Disabling_WhatIsNotThere_IsFine() {
        autostart.Disable();

        Assert.False(autostart.IsEnabled());
        Assert.Null(ApprovedValue());
    }

    [Fact]
    public void AnEntryThatWasSwitchedOff_IsSwitchedBackOnWithItsOwnPath_EvenWithoutAgentNextToTheApp() {
        const string installed = @"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe";
        using (RegistryKey run = scratch.Create("Run")) {
            run.SetValue(Name, installed);
        }
        File.Delete(agentPath);
        Assert.True(autostart.IsEnabled());

        autostart.Disable();
        Assert.False(autostart.IsEnabled());
        string? problem = autostart.Enable();

        Assert.Null(problem);
        Assert.True(autostart.IsEnabled());
        Assert.Equal(installed, RunValue());
    }

    [Fact]
    public void Enabling_DoesNotRewriteAnExistingEntryWithThePathNextToTheApp() {
        using (RegistryKey run = scratch.Create("Run")) {
            run.SetValue(Name, @"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe");
        }

        autostart.Enable();

        Assert.Equal(@"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe", RunValue());
    }

    [Theory]
    [InlineData(@"""C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe""", @"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe")]
    [InlineData(@"""C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe"" --quiet", @"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe")]
    [InlineData(@"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe", @"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.exe")]
    [InlineData(@"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.EXE --quiet", @"C:\Program Files\4MagicKeysPlus\4MagicKeysPlusAgent.EXE")]
    public void TheRegisteredPath_IsTheProgramOfTheRunValue(string command, string expected) {
        using (RegistryKey run = scratch.Create("Run")) {
            run.SetValue(Name, command);
        }

        Assert.Equal(expected, autostart.RegisteredPath());
    }

    [Fact]
    public void WithoutTheRunValue_ThereIsNoRegisteredPath() {
        Assert.Null(autostart.RegisteredPath());
    }

    [Fact]
    public void AnEntry_SwitchedOffInTaskManager_IsOff() {
        autostart.Enable();
        Approve(0x03, 0, 0, 0, 0xCA, 0x1B, 0x04, 0xDF, 0xB7, 0x57, 0xD8, 0x01);

        Assert.False(autostart.IsEnabled());
    }

    [Fact]
    public void AnEntry_ApprovedInTaskManager_IsOn() {
        autostart.Enable();
        Approve(0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.True(autostart.IsEnabled());
    }

    [Fact]
    public void Enabling_SwitchesTheEntryBackOnInTaskManager() {
        autostart.Enable();
        Approve(0x03, 0, 0, 0, 0xCA, 0x1B, 0x04, 0xDF, 0xB7, 0x57, 0xD8, 0x01);

        autostart.Enable();

        Assert.True(autostart.IsEnabled());
        Assert.Equal(0x02, ApprovedValue()![0]);
    }

    [Fact]
    public void Enabling_DoesNotCreateTheTaskManagerFlag() {
        autostart.Enable();

        Assert.Null(ApprovedValue());
    }
}
