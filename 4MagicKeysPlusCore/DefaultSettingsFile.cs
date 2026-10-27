using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace _4MagicKeysPlus.Core;

// The default settings of a model as they are shipped: a JSON file named by the model (A1843.json), the same layout as the
// registry has. Source and Target are the raw values of the driver as hex texts ("0x0100003A"); comments and trailing commas
// are allowed in the file.
//
//   { "model": "A1843", "fnLock": true, "rules": [ { "source": "0x0100003A", "target": "0x01000070", "fnLockAware": true, "comment": "Fn+F1 -> Brightness Down" } ] }
public static class DefaultSettingsFile {
    // The driver keeps no more rules than this; the rest would be dropped without a word.
    public const int MaxRules = 128;

    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // A FormatException says what is wrong with the file, when it is not a file of default settings.
    public static DefaultSettings Load(string path) {
        try {
            return Parse(File.ReadAllText(path));
        } catch (FormatException exception) {
            throw new FormatException($"{Path.GetFileName(path)}: {exception.Message}", exception);
        }
    }

    public static DefaultSettings Parse(string json) {
        FileModel? file;
        try {
            file = JsonSerializer.Deserialize<FileModel>(json, Options);
        } catch (JsonException exception) {
            throw new FormatException(exception.Message, exception);
        }
        if (file is null) {
            throw new FormatException("The file is empty.");
        }

        if (file.Model is null || !Enum.GetNames<KeyboardProduct>().Contains(file.Model, StringComparer.Ordinal)) {
            throw new FormatException($"'{file.Model}' is not a known model ({string.Join(", ", Enum.GetNames<KeyboardProduct>())}).");
        }

        List<FileRule?> fileRules = file.Rules ?? [];
        if (fileRules.Count > MaxRules) {
            throw new FormatException($"There are {fileRules.Count} rules, the driver keeps {MaxRules} at most.");
        }

        List<DefaultRule> rules = [];
        HashSet<uint> sources = [];
        foreach (FileRule? rule in fileRules) {
            if (rule is null) {
                throw new FormatException("A rule is null.");
            }

            uint source = ParseValue(rule.Source, "source");
            if (!sources.Add(source)) {
                throw new FormatException($"The source 0x{source:X8} has more than one rule.");
            }

            rules.Add(new DefaultRule(source, ParseValue(rule.Target, "target"), rule.FnLockAware, string.IsNullOrEmpty(rule.Comment) ? null : rule.Comment));
        }

        return new DefaultSettings(Enum.Parse<KeyboardProduct>(file.Model!), file.FnLock, rules);
    }

    private static uint ParseValue(string? text, string name) {
        if (text is not null
            && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && text.Length is > 2 and <= 10
            && uint.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value)) {
            return value;
        }

        throw new FormatException($"'{text}' is not a value of {name}: expected 0x and up to eight hex digits.");
    }

    // What the serializer fills in; a null is what a JSON null leaves.
    private sealed class FileModel {
        public required string? Model {
            get; init;
        }

        public bool FnLock {
            get; init;
        }

        public List<FileRule?>? Rules {
            get; init;
        }
    }

    private sealed class FileRule {
        public required string? Source {
            get; init;
        }

        public required string? Target {
            get; init;
        }

        public bool FnLockAware {
            get; init;
        }

        public string? Comment {
            get; init;
        }
    }
}
