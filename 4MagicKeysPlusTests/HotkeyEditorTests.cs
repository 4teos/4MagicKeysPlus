using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

internal sealed class FakeHotkeyStore : IHotkeyStore {
    public List<HotkeyEntry> Stored { get; } = [];
    public Exception? Failure { get; set; }
    public int Saves { get; private set; }

    public IReadOnlyList<HotkeyEntry> Load() {
        return Stored;
    }

    public IReadOnlyList<HotkeyEntry> Save(IReadOnlyList<HotkeyEntry> saved, IReadOnlyList<HotkeyEntry> wanted) {
        if (Failure is not null) {
            throw Failure;
        }
        Saves++;
        return HotkeysPlan.Create(saved, wanted).Result;
    }
}

internal sealed class FakeAgentLink : IAgentLink {
    public AgentResponse? Reply { get; set; } = AgentResponse.Success([]);
    public int Reloads { get; private set; }

    public Task<AgentResponse?> ReloadAsync() {
        Reloads++;
        return Task.FromResult(Reply);
    }

    public Task<IAgentPause?> PauseAsync() {
        return Task.FromResult<IAgentPause?>(null);
    }
}

public class HotkeyEditorTests {
    private static HotkeyEntry Entry(string? name = null, uint virtualKey = 0x7F, uint modifiers = 0) {
        return new HotkeyEntry(name, "Calc", @"C:\calc.exe", virtualKey, modifiers);
    }

    [Fact]
    public async Task ASavedChange_IsHandedToTheAgent() {
        FakeHotkeyStore store = new();
        FakeAgentLink agent = new() { Reply = AgentResponse.Success([new HotkeyRegistration("Hotkey_00_7F", 0)]) };
        HotkeyEditor editor = new(store, agent);

        HotkeyApplyResult result = await editor.ApplyAsync([Entry()]);

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.Equal((1, 1), (store.Saves, agent.Reloads));
        Assert.Equal("Hotkey_00_7F", Assert.Single(editor.Entries).RegistryName);
        Assert.True(result.AgentRunning);
    }

    [Fact]
    public async Task WhenTheAgentIsNotRunning_TheChangeIsSavedAndReportedAsNotApplied() {
        HotkeyEditor editor = new(new FakeHotkeyStore(), new FakeAgentLink { Reply = null });

        HotkeyApplyResult result = await editor.ApplyAsync([Entry()]);

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.False(result.AgentRunning);
        Assert.Single(editor.Entries);
    }

    [Fact]
    public async Task HotkeysTheAgentCouldNotRegister_AreReportedAndStaySaved() {
        FakeAgentLink agent = new() {
            Reply = AgentResponse.Success([new HotkeyRegistration("Hotkey_00_7F", 0), new HotkeyRegistration("Hotkey_00_80", 1409)]),
        };
        HotkeyEditor editor = new(new FakeHotkeyStore(), agent);

        HotkeyApplyResult result = await editor.ApplyAsync([Entry(), Entry(virtualKey: 0x80)]);

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.Equal(1409, result.Registrations.Single(r => !r.Succeeded).Win32Error);
        Assert.Equal(2, editor.Entries.Count);
        Assert.True(result.AgentRunning);
    }

    [Fact]
    public async Task AFailedSave_ChangesNothingAndDoesNotTouchTheAgent() {
        FakeAgentLink agent = new();
        HotkeyEditor editor = new(new FakeHotkeyStore { Failure = new UnauthorizedAccessException("denied") }, agent);

        HotkeyApplyResult result = await editor.ApplyAsync([Entry()]);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Empty(editor.Entries);
        Assert.Equal(0, agent.Reloads);
    }

    [Fact]
    public async Task AFailureReplyFromTheAgent_IsAWarningWithItsMessage() {
        HotkeyEditor editor = new(new FakeHotkeyStore(), new FakeAgentLink { Reply = AgentResponse.Failure("boom") });

        HotkeyApplyResult result = await editor.ApplyAsync([Entry()]);

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.Equal("boom", result.Message);
    }

    [Fact]
    public void FindConflict_MatchesOnKeyAndModifiersNotOnTheSubkey() {
        HotkeyEntry calc = Entry("Launch_00_7F");

        Assert.Same(calc, HotkeyEditor.FindConflict([calc], 0x7F, 0));
        Assert.Null(HotkeyEditor.FindConflict([calc], 0x7F, 0x02));
        Assert.Null(HotkeyEditor.FindConflict([calc], 0x80, 0));
    }
}
