using System.IO;
using System.Security;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

public sealed class DefaultsEditor {
    private readonly Guid profileId;
    private readonly KeyboardProduct product;
    private readonly IDefaultSettings defaults;
    private readonly KeyRemapEditor rules;
    private readonly Func<IReadOnlyList<KeyRule>> readRules;
    private readonly IDriverControl driver;

    internal DefaultsEditor(Guid profileId, KeyboardProduct product, IDefaultSettings defaults, KeyRemapEditor rules, Func<IReadOnlyList<KeyRule>> readRules, IDriverControl driver) {
        this.profileId = profileId;
        this.product = product;
        this.defaults = defaults;
        this.rules = rules;
        this.readRules = readRules;
        this.driver = driver;
    }

    public event Action? Changed;

    public bool IsAvailable => defaults.Has(product);

    public ApplyResult Reset() {
        ApplyResult? failure = null;
        try {
            if (!defaults.CopyToProfile(profileId, product)) {
                failure = new ApplyResult(ApplyOutcome.Failed, Strings.ResetDefaultsUnavailable);
            }
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException or InvalidOperationException) {
            failure = new ApplyResult(ApplyOutcome.Failed, string.Format(Strings.ApplySaveFailed, exception.Message));
        }

        // Whatever came of the copy, the editor has to show what is in the registry now.
        rules.Replace(readRules());
        if (failure is not null) {
            return failure;
        }

        Changed?.Invoke();
        return KeyRemapEditor.ReloadDriver(driver);
    }
}
