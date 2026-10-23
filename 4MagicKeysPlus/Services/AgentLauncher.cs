using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using _4MagicKeysPlus.Properties;

namespace _4MagicKeysPlus.Services;

internal static class AgentLauncher {
    private const string AgentFileName = "4MagicKeysPlusAgent.exe";

    // The agent is installed next to this app.
    public static string AgentPath => Path.Combine(AppContext.BaseDirectory, AgentFileName);

    // The agent next to this app or, when there is none (the app runs from its build folder while
    // developing), the one the autostart value points at.
    public static string ResolvePath() {
        if (File.Exists(AgentPath)) {
            return AgentPath;
        }

        string? registered = new RegistryAgentAutostart().RegisteredPath();
        return registered is not null && File.Exists(registered) ? registered : AgentPath;
    }

    // Starts the agent and answers what went wrong, or null. This app runs elevated and a child
    // would inherit that, but the agent has to run with the user's normal rights (its hotkeys
    // would not reach the windows of other normal programs otherwise), so Explorer starts it.
    public static string? TryStart() {
        string path = ResolvePath();
        if (!File.Exists(path)) {
            return string.Format(Strings.HotkeyAgentNotFound, path);
        }

        try {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\""));
            return null;
        } catch (Exception exception) when (exception is Win32Exception or InvalidOperationException) {
            return exception.Message;
        }
    }

    // Waits for a started agent to answer on its pipe; false when it has not within the polls.
    public static async Task<bool> WaitUntilRunningAsync(Func<Task<bool>> isRunning, int polls = 10, TimeSpan? interval = null) {
        for (int poll = 0; poll < polls; poll++) {
            if (await isRunning()) {
                return true;
            }
            await Task.Delay(interval ?? TimeSpan.FromMilliseconds(500));
        }
        return false;
    }
}
