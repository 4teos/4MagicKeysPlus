using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

// The rule list being edited on the Keys screen. Lives for the whole app session (owned by
// MainWindow), so edits survive switching between the screens.
public sealed class KeyRemapEditor {
    private IReadOnlyList<KeyRule> rules;

    public KeyRemapEditor() : this(DriverStatus.ReadKeyRemapConfig().Rules) {
    }

    internal KeyRemapEditor(IReadOnlyList<KeyRule> initialRules) {
        rules = initialRules;
    }

    internal IReadOnlyList<KeyRule> Rules => rules;

    // FnLock is not edited here and can change at any time, so it is read afresh.
    public DriverKeyRemapConfig Config => new(rules, DriverStatus.ReadFnLockEnabled());

    public void SetMapping(PhysicalKey source, RemapTarget target, bool fnLayer, bool fnLockAware) {
        KeyRule rule = DriverRuleEncoder.CreateRule(source, target, fnLayer, fnLockAware)
            ?? throw new InvalidOperationException($"The driver would drop the rule {source} -> {target.DisplayName} (Fn layer: {fnLayer})");
        Commit(new DriverKeyRemapConfig(rules, false).WithRule(rule).Rules);
    }

    public void RemoveMappings(PhysicalKey key) {
        Commit(new DriverKeyRemapConfig(rules, false).WithoutRules(key).Rules);
    }

    // The one place the rule list changes: writing it to the registry goes here.
    private void Commit(IReadOnlyList<KeyRule> newRules) {
        rules = newRules;
    }
}
