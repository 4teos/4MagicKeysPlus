using System.ComponentModel;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

internal interface IHotkeyRegistrar {
    bool RegisterHotKey(HotkeyEntry entry, out int win32Error);
    void UnregisterAll();
}

// Owns the registered hotkeys. The registrar's window belongs to the UI thread, so everything
// here has to be called from it.
internal sealed class HotkeyManager {
    private readonly IHotkeyRegistrar registrar;
    private readonly Func<List<HotkeyEntry>> load;
    private List<HotkeyEntry> entries = [];
    private Dictionary<int, string> pathById = [];

    public HotkeyManager(IHotkeyRegistrar registrar, Func<List<HotkeyEntry>> load) {
        this.registrar = registrar;
        this.load = load;
    }

    public IReadOnlyList<HotkeyEntry> Entries => entries;

    // Reads the configuration again and registers it from scratch, which also ends a pause.
    // A hotkey that fails to register does not stop the others.
    public IReadOnlyList<HotkeyRegistration> Reload() {
        entries = load();
        pathById = entries.ToDictionary(entry => entry.HotkeyId, entry => entry.ExecutablePath);
        return RegisterAll();
    }

    public void Pause() {
        registrar.UnregisterAll();
    }

    public IReadOnlyList<HotkeyRegistration> Resume() {
        return RegisterAll();
    }

    public string? FindPath(int hotkeyId) {
        return pathById.GetValueOrDefault(hotkeyId);
    }

    // One line per failed registration, for the notice shown when the agent starts.
    public IReadOnlyList<string> DescribeFailures(IEnumerable<HotkeyRegistration> registrations) {
        List<string> lines = [];
        foreach (HotkeyRegistration registration in registrations.Where(r => !r.Succeeded)) {
            HotkeyEntry? entry = entries.FirstOrDefault(e => e.RegistryName == registration.RegistryName);
            string reason = new Win32Exception(registration.Win32Error).Message;
            lines.Add(entry is null
                ? $"{registration.RegistryName}: {reason}"
                : $"{entry.Name} ({HotkeyText.Format(entry.VirtualKey, entry.Modifiers)}): {reason}");
        }
        return lines;
    }

    private List<HotkeyRegistration> RegisterAll() {
        registrar.UnregisterAll();
        List<HotkeyRegistration> results = [];
        foreach (HotkeyEntry entry in entries) {
            if (registrar.RegisterHotKey(entry, out int win32Error)) {
                results.Add(new HotkeyRegistration(entry.RegistryName, 0));
            } else {
                Logger.Warn($"Failed to register hotkey '{entry.Name}' (vk=0x{entry.VirtualKey:X2}, mod=0x{entry.Modifiers:X2}), Win32 error {win32Error}.");
                results.Add(new HotkeyRegistration(entry.RegistryName, win32Error));
            }
        }
        return results;
    }
}
