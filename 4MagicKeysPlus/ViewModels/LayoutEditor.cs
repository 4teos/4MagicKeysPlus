using System.IO;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

public sealed class LayoutEditor {
    private readonly ILayoutStore store;

    internal LayoutEditor(ILayoutStore store) {
        this.store = store;
    }

    public LayoutStandard Standard => store.Read();

    public ApplyResult Set(LayoutStandard standard) {
        try {
            store.Write(standard);
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException) {
            return new ApplyResult(ApplyOutcome.Failed, string.Format(Strings.ApplySaveFailed, exception.Message));
        }

        return new ApplyResult(ApplyOutcome.Applied, Strings.ApplyApplied);
    }
}
