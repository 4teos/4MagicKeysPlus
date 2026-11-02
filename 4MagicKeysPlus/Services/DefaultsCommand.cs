using System.IO;
using System.Runtime.InteropServices;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlus.Services;

// "4MagicKeysPlus.exe --install-defaults [folder]" puts the default settings into the registry with no window. The exit code is 0 when they all
// went in and 1 when something did not; what happened goes to the console the app was started from, or to the file it is redirected to.
internal static class DefaultsCommand {
    public const string Switch = "--install-defaults";

    private const int AttachParentProcess = -1;

    public static bool Asks(string[] args) {
        return args.Length > 0 && string.Equals(args[0], Switch, StringComparison.OrdinalIgnoreCase);
    }

    public static int RunFromConsole(string[] args) {
        // A window-less app has no console of its own; the one that started it is used while there is one.
        AttachConsole(AttachParentProcess);
        return Run(args, AppContext.BaseDirectory, new RegistryDefaultSettings(), Console.Out);
    }

    public static int Run(string[] args, string appFolder, RegistryDefaultSettings target, TextWriter output) {
        string folder = args.Length > 1 ? args[1] : Path.Combine(appFolder, "defaults");
        DefaultsInstallResult result = DefaultSettingsInstaller.Install(folder, target);

        foreach (KeyboardProduct model in result.Installed) {
            output.WriteLine($"{model}: the default settings are in the registry.");
        }
        foreach (string problem in result.Problems) {
            output.WriteLine(problem);
        }
        return result.Problems.Count == 0 ? 0 : 1;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);
}
