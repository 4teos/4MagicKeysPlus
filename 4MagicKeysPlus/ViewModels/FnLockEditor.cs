using System.IO;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

// The Fn Lock switch above the keyboard. Lives for the whole app session (owned by MainWindow);
// a change is saved to the registry and handed to the driver at once, like a rule.
public sealed class FnLockEditor {
    private readonly IFnLockStore store;
    private readonly IDriverControl driver;

    public FnLockEditor() : this(new RegistryFnLockStore(), new DriverControl()) {
    }

    internal FnLockEditor(IFnLockStore store, IDriverControl driver) {
        this.store = store;
        this.driver = driver;
    }

    // Raised once the new state is saved, for the header badge that shows it.
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
