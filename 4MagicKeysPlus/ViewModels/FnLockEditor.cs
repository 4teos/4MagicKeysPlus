using System.IO;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

public sealed class FnLockEditor {
    private readonly IFnLockStore store;
    private readonly IDriverControl driver;

    internal FnLockEditor(IFnLockStore store, IDriverControl driver) {
        this.store = store;
        this.driver = driver;
    }

    public event Action? Changed;

    public bool IsEnabled => store.Read();

    public ApplyResult Set(bool enabled) {
        try {
            store.Write(enabled);
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException) {
            return new ApplyResult(ApplyOutcome.Failed, string.Format(Strings.ApplySaveFailed, exception.Message));
        }

        Changed?.Invoke();
        return KeyRemapEditor.ReloadDriver(driver);
    }
}
