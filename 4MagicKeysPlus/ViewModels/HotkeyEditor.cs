using System.IO;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

// Entries are what is saved, with the subkey name each one got. Registrations are what the agent
// made of them; empty when it did not answer.
public sealed record HotkeyApplyResult(ApplyOutcome Outcome, string Message, IReadOnlyList<HotkeyEntry> Entries, IReadOnlyList<HotkeyRegistration> Registrations, bool AgentRunning);

// The hotkey list on the Quick launch screen. Lives for the whole app session (owned by
// MainWindow). Every change is saved to the registry and the agent is told at once.
public sealed class HotkeyEditor {
    private readonly IHotkeyStore store;
    private readonly IAgentLink agent;
    private IReadOnlyList<HotkeyEntry> entries;

    public HotkeyEditor() : this(new RegistryHotkeyStore(), new AgentLink()) {
    }

    internal HotkeyEditor(IHotkeyStore store, IAgentLink agent) {
        this.store = store;
        this.agent = agent;
        entries = store.Load();
    }

    public IReadOnlyList<HotkeyEntry> Entries => entries;

    // Releases the agent's hotkeys while a key is being captured; null when the agent is not running.
    internal Task<IAgentPause?> PauseAgentAsync() {
        return agent.PauseAsync();
    }

    // The entry that already has this combination among those, if any.
    public static HotkeyEntry? FindConflict(IEnumerable<HotkeyEntry> others, uint virtualKey, uint modifiers) {
        return others.FirstOrDefault(entry => entry.SameCombination(virtualKey, modifiers));
    }

    // Saves wanted as the whole list. Nothing changes when saving fails; a saved list the agent
    // did not take (not running, or hotkeys it could not register) still stays saved.
    public async Task<HotkeyApplyResult> ApplyAsync(IReadOnlyList<HotkeyEntry> wanted) {
        try {
            entries = store.Save(entries, wanted);
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException) {
            return new HotkeyApplyResult(ApplyOutcome.Failed, string.Format(Strings.ApplySaveFailed, exception.Message), entries, [], AgentRunning: true);
        }

        AgentResponse? reply = await agent.ReloadAsync();
        if (reply is null) {
            return new HotkeyApplyResult(ApplyOutcome.Warning, Strings.HotkeyAgentNotRunning, entries, [], AgentRunning: false);
        }
        if (!reply.Ok) {
            return new HotkeyApplyResult(ApplyOutcome.Warning, reply.Message ?? Strings.HotkeyAgentNotRunning, entries, [], AgentRunning: true);
        }

        IReadOnlyList<HotkeyRegistration> registrations = reply.Hotkeys ?? [];
        int failed = registrations.Count(registration => !registration.Succeeded);
        return failed == 0
            ? new HotkeyApplyResult(ApplyOutcome.Applied, Strings.ApplyApplied, entries, registrations, AgentRunning: true)
            : new HotkeyApplyResult(ApplyOutcome.Warning, string.Format(Strings.HotkeyNotRegistered, failed), entries, registrations, AgentRunning: true);
    }
}
