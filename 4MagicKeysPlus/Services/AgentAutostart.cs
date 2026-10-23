using System.IO;
using Microsoft.Win32;
using _4MagicKeysPlus.Properties;

namespace _4MagicKeysPlus.Services;

internal interface IAgentAutostart {
    bool IsEnabled();

    // Answers what went wrong, or null.
    string? Enable();

    void Disable();
}

// The agent starts at logon through the Run key of the user. Switching it off does not delete
// that value but sets the flag Task Manager keeps in StartupApproved, so it can be switched back
// on without knowing where the agent is (it is often not next to this app, which runs from its
// build folder while developing). An entry counts as on only while that flag is not set.
internal sealed class RegistryAgentAutostart : IAgentAutostart {
    internal const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    internal const string ValueName = "4MagicKeysPlusAgent";

    // What Windows itself writes when an entry is switched on or off in Task Manager (without the time of the change).
    private static readonly byte[] ApprovedEnabled = [0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] ApprovedDisabled = [0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private readonly RegistryKey root;
    private readonly string runPath;
    private readonly string approvedPath;
    private readonly string agentPath;

    public RegistryAgentAutostart() : this(Registry.CurrentUser, RunPath, ApprovedPath, AgentLauncher.AgentPath) {
    }

    // The root and paths are parameters so that tests can use scratch keys of their own.
    public RegistryAgentAutostart(RegistryKey root, string runPath, string approvedPath, string agentPath) {
        this.root = root;
        this.runPath = runPath;
        this.approvedPath = approvedPath;
        this.agentPath = agentPath;
    }

    public bool IsEnabled() {
        if (!HasRunValue()) {
            return false;
        }

        using RegistryKey? approved = root.OpenSubKey(approvedPath);
        return approved?.GetValue(ValueName) is not byte[] { Length: > 0 } flags || (flags[0] & 1) == 0;
    }

    // An entry that is there keeps its path: it is where the agent really is.
    public string? Enable() {
        if (!HasRunValue()) {
            if (!File.Exists(agentPath)) {
                return string.Format(Strings.HotkeyAgentNotFound, agentPath);
            }

            using RegistryKey run = root.CreateSubKey(runPath, writable: true);
            run.SetValue(ValueName, $"\"{agentPath}\"", RegistryValueKind.String);
        }

        using RegistryKey? approved = root.OpenSubKey(approvedPath, writable: true);
        if (approved?.GetValue(ValueName) is byte[]) {
            approved.SetValue(ValueName, ApprovedEnabled, RegistryValueKind.Binary);
        }
        return null;
    }

    public void Disable() {
        if (!HasRunValue()) {
            return;
        }

        using RegistryKey approved = root.CreateSubKey(approvedPath, writable: true);
        approved.SetValue(ValueName, ApprovedDisabled, RegistryValueKind.Binary);
    }

    // The program the Run value starts, or null when there is no value.
    public string? RegisteredPath() {
        using RegistryKey? run = root.OpenSubKey(runPath);
        if (run?.GetValue(ValueName) is not string command) {
            return null;
        }

        command = command.Trim();
        if (command.StartsWith('"')) {
            int end = command.IndexOf('"', 1);
            return end > 0 ? command[1..end] : command[1..];
        }

        int extension = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return extension >= 0 ? command[..(extension + 4)] : command;
    }

    private bool HasRunValue() {
        using RegistryKey? run = root.OpenSubKey(runPath);
        return run?.GetValue(ValueName) is string;
    }
}
