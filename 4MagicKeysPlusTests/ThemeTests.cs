using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlusTests;

// Colors live in Themes\*.xaml only. A DynamicResource that names a missing key fails silently
// at runtime (the control just stays uncolored), so the keys are checked here.
public class ThemeTests {
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string AppDirectory {
        get {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "4MagicKeysPlus.sln"))) {
                directory = directory.Parent;
            }
            return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found"), "4MagicKeysPlus");
        }
    }

    private static IEnumerable<string> ThemeFiles => Directory.GetFiles(Path.Combine(AppDirectory, "Themes"), "*.xaml");

    private static IEnumerable<string> SourceFiles => Directory
        .EnumerateFiles(AppDirectory, "*.*", SearchOption.AllDirectories)
        .Where(file => file.EndsWith(".xaml") || file.EndsWith(".cs"))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
            && !file.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}"));

    private static HashSet<string> KeysOf(string themeFile) {
        return [.. XDocument.Load(themeFile).Descendants().Select(element => (string?)element.Attribute(Xaml + "Key")).OfType<string>()];
    }

    private static HashSet<string> UsedKeys() {
        HashSet<string> keys = [];
        foreach (string file in SourceFiles.Where(file => file.EndsWith(".xaml"))) {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\{DynamicResource\s+(\w+)\}")) {
                keys.Add(match.Groups[1].Value);
            }
        }
        keys.UnionWith(typeof(ThemeKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!));
        return keys;
    }

    [Fact]
    public void EveryUsedKey_IsDefinedInEveryTheme() {
        HashSet<string> used = UsedKeys();
        Assert.NotEmpty(used);

        foreach (string themeFile in ThemeFiles) {
            Assert.Empty(used.Except(KeysOf(themeFile)));
        }
    }

    [Fact]
    public void EveryTheme_DefinesTheSameKeys() {
        List<HashSet<string>> themes = [.. ThemeFiles.Select(KeysOf)];

        Assert.All(themes, keys => Assert.True(keys.SetEquals(themes[0])));
    }

    [Fact]
    public void NoDefinedKey_IsUnused() {
        Assert.Empty(KeysOf(ThemeFiles.First()).Except(UsedKeys()));
    }

    [Fact]
    public void NoColorLiteral_OutsideTheThemes() {
        foreach (string file in SourceFiles) {
            Assert.DoesNotMatch(@"#[0-9A-Fa-f]{6,8}\b|Color\.From|Brushes\.", File.ReadAllText(file));
        }
    }
}
