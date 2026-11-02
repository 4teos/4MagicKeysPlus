using System.Security;

namespace _4MagicKeysPlus.Core;

public sealed record DefaultsInstallResult(IReadOnlyList<KeyboardProduct> Installed, IReadOnlyList<string> Problems);

// All or nothing, so that a damaged file does not leave a mix of old and new defaults; the profiles of the keyboards are never touched.
public static class DefaultSettingsInstaller {
    public static DefaultsInstallResult Install(string folder, RegistryDefaultSettings target) {
        List<string> problems = [];
        List<DefaultSettings> files = [];

        string[] paths = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json") : [];
        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
        if (paths.Length == 0) {
            problems.Add($"{folder}: there are no .json files of default settings.");
        }

        foreach (string path in paths) {
            try {
                DefaultSettings defaults = DefaultSettingsFile.Load(path);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), defaults.Model.ToString(), StringComparison.OrdinalIgnoreCase)) {
                    problems.Add($"{Path.GetFileName(path)}: the file is of the model {defaults.Model}, so it has to be named {defaults.Model}.json.");
                } else {
                    files.Add(defaults);
                }
            } catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException) {
                problems.Add(exception.Message);
            }
        }

        if (problems.Count > 0) {
            return new DefaultsInstallResult([], problems);
        }

        List<KeyboardProduct> installed = [];
        foreach (DefaultSettings defaults in files) {
            try {
                target.Write(defaults);
                installed.Add(defaults.Model);
            } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException) {
                problems.Add($"{defaults.Model}: the registry could not be written: {exception.Message}");
            }
        }
        return new DefaultsInstallResult(installed, problems);
    }
}
