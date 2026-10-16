using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using _4MagicKeysPlus.Properties;

namespace _4MagicKeysPlus.Services;

internal static class AgentLauncher {
    private const string AgentFileName = "4MagicKeysPlusAgent.exe";

    // The agent is installed next to this app.
    public static string AgentPath => Path.Combine(AppContext.BaseDirectory, AgentFileName);

    // Starts the agent and answers what went wrong, or null. This app runs elevated and a child
    // would inherit that, but the agent has to run with the user's normal rights (its hotkeys
    // would not reach the windows of other normal programs otherwise), so Explorer starts it.
    public static string? TryStart() {
        string path = AgentPath;
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
}
