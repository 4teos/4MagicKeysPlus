using System.ComponentModel;
using System.IO;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

public enum ApplyOutcome {
    Applied,
    // The rules are saved but the driver did not take them all, or was not told: it picks them
    // up the next time a keyboard connects.
    Warning,
    // Nothing was saved.
    Failed,
}

public sealed record ApplyResult(ApplyOutcome Outcome, string Message);

// The rule list of one keyboard, being edited on the Keys screen. Lives for the whole app session (see
// ProfileEditors), so the screens share it. Every change is saved to the registry and handed to the
// driver at once.
public sealed class KeyRemapEditor {
    private readonly IRulesStore store;
    private readonly IDriverControl driver;
    private readonly IFnLockStore fnLock;
    private IReadOnlyList<KeyRule> rules;

    // fnLock is the Fn Lock of the same keyboard, which is only read here.
    internal KeyRemapEditor(IReadOnlyList<KeyRule> initialRules, IRulesStore store, IDriverControl driver, IFnLockStore fnLock) {
        rules = initialRules;
        this.store = store;
        this.driver = driver;
        this.fnLock = fnLock;
    }

    internal IReadOnlyList<KeyRule> Rules => rules;

    // FnLock is not edited here and can change at any time, so it is read afresh.
    public DriverKeyRemapConfig Config => new(rules, fnLock.Read());

    public ApplyResult SetMapping(PhysicalKey source, RemapTarget target, bool fnLayer, bool fnLockAware) {
        KeyRule rule = DriverRuleEncoder.CreateRule(source, target, fnLayer, fnLockAware)
            ?? throw new InvalidOperationException($"The driver would drop the rule {source} -> {target.DisplayName} (Fn layer: {fnLayer})");
        return Commit(new DriverKeyRemapConfig(rules, false).WithRule(rule).Rules);
    }

    public ApplyResult RemoveMappings(PhysicalKey key) {
        return Commit(new DriverKeyRemapConfig(rules, false).WithoutRules(key).Rules);
    }

    private ApplyResult Commit(IReadOnlyList<KeyRule> wanted) {
        try {
            rules = store.Save(rules, wanted);
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException) {
            return new ApplyResult(ApplyOutcome.Failed, string.Format(Strings.ApplySaveFailed, exception.Message));
        }

        return ReloadDriver(driver);
    }

    // Tells the driver to read what has been saved, and what came of it.
    internal static ApplyResult ReloadDriver(IDriverControl driver) {
        try {
            DriverReloadResult? reload = driver.Reload();
            if (reload is null) {
                return new ApplyResult(ApplyOutcome.Warning, Strings.ApplySavedNotApplied);
            }
            if (reload.RulesAccepted < reload.RulesRead) {
                return new ApplyResult(ApplyOutcome.Warning, string.Format(Strings.ApplyIgnored, reload.RulesRead - reload.RulesAccepted));
            }
            return new ApplyResult(ApplyOutcome.Applied, Strings.ApplyApplied);
        } catch (Win32Exception exception) {
            return new ApplyResult(ApplyOutcome.Warning, string.Format(Strings.ApplyDriverError, exception.Message));
        }
    }
}
