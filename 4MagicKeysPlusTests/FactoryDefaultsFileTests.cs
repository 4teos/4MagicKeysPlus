using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusTests;

public sealed class FactoryDefaultsFileTests {
    private const string Complete = """
        {
          "model": "A1843",
          "fnLock": true,
          "rules": [
            { "source": "0x0100003A", "target": "0x01000070", "fnLockAware": true, "comment": "Fn+F1 -> Brightness Down" },
            { "source": "0x00000068", "target": "0x00000046" }
          ]
        }
        """;

    [Fact]
    public void ACompleteFile_IsReadAsItIs() {
        FactoryDefaults defaults = FactoryDefaultsFile.Parse(Complete);

        Assert.Equal(KeyboardProduct.A1843, defaults.Model);
        Assert.True(defaults.FnLock);
        Assert.Equal(
            [
                new FactoryRule(0x0100003A, 0x01000070, true, "Fn+F1 -> Brightness Down"),
                new FactoryRule(0x00000068, 0x00000046, false, null),
            ],
            defaults.Rules);
    }

    [Fact]
    public void CommentsAndTrailingCommas_AreAllowed() {
        FactoryDefaults defaults = FactoryDefaultsFile.Parse("""
            // The defaults of the A1644.
            {
              "model": "A1644", /* the model */
              "rules": [
                { "source": "0x00000068", "target": "0x00000046", },
              ],
            }
            """);

        Assert.Equal(KeyboardProduct.A1644, defaults.Model);
        Assert.Single(defaults.Rules);
    }

    [Fact]
    public void WithoutFnLockAndRules_FnLockIsOffAndThereAreNoRules() {
        FactoryDefaults defaults = FactoryDefaultsFile.Parse("""{ "model": "A1843" }""");

        Assert.False(defaults.FnLock);
        Assert.Empty(defaults.Rules);
    }

    [Fact]
    public void ABlankCommentIsNoComment() {
        FactoryDefaults defaults = FactoryDefaultsFile.Parse("""{ "model": "A1843", "rules": [ { "source": "0x1", "target": "0x2", "comment": "" } ] }""");

        Assert.Null(defaults.Rules[0].Comment);
    }

    [Theory]
    [InlineData("0x1", 0x1u)]
    [InlineData("0X0000006a", 0x6Au)]
    [InlineData("0xFFFFFFFF", 0xFFFFFFFFu)]
    public void TheValuesAreHexTextsOfUpToEightDigits(string text, uint expected) {
        FactoryDefaults defaults = FactoryDefaultsFile.Parse($$"""{ "model": "A1843", "rules": [ { "source": "{{text}}", "target": "0x2" } ] }""");

        Assert.Equal(expected, defaults.Rules[0].Source);
    }

    [Theory]
    [InlineData("58")]
    [InlineData("0x")]
    [InlineData("0xG1")]
    [InlineData("0x100000000")]
    [InlineData("-0x1")]
    [InlineData("0x 1")]
    [InlineData("")]
    public void AValueThatIsNotHex_IsRefused(string text) {
        FormatException exception = Assert.Throws<FormatException>(
            () => FactoryDefaultsFile.Parse($$"""{ "model": "A1843", "rules": [ { "source": "{{text}}", "target": "0x2" } ] }"""));

        Assert.Contains("source", exception.Message);
    }

    [Theory]
    [InlineData("""{ "model": "A9999" }""")]
    [InlineData("""{ "model": "a1843" }""")]
    [InlineData("""{ "model": "1" }""")]
    [InlineData("""{ "model": null }""")]
    [InlineData("""{ "fnLock": true }""")]
    [InlineData("""{ "model": "A1843", "fnlock": true }""")]
    [InlineData("""{ "model": "A1843", "fnLock": "yes" }""")]
    [InlineData("""{ "model": "A1843", "rules": [ { "source": "0x1" } ] }""")]
    [InlineData("""{ "model": "A1843", "rules": [ { "source": null, "target": "0x2" } ] }""")]
    [InlineData("""{ "model": "A1843", "rules": [ null ] }""")]
    [InlineData("""{ "model": "A1843", "rules": [ { "source": "0x1", "target": "0x2", "fnLockAwre": true } ] }""")]
    [InlineData("""{ "model": "A1843", "rules": [ { "source": "0x1", "target": "0x2" }, { "source": "0x1", "target": "0x3" } ] }""")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("null")]
    public void AFileThatIsNotOfFactoryDefaults_IsRefused(string json) {
        Assert.Throws<FormatException>(() => FactoryDefaultsFile.Parse(json));
    }

    [Fact]
    public void TheNumberOfRules_IsLimitedLikeTheDriversIs() {
        string Rules(int count) {
            return string.Join(", ", Enumerable.Range(1, count).Select(i => $$"""{ "source": "0x{{i:X}}", "target": "0x2" }"""));
        }

        Assert.Equal(FactoryDefaultsFile.MaxRules, FactoryDefaultsFile.Parse($$"""{ "model": "A1843", "rules": [ {{Rules(FactoryDefaultsFile.MaxRules)}} ] }""").Rules.Count);
        Assert.Throws<FormatException>(() => FactoryDefaultsFile.Parse($$"""{ "model": "A1843", "rules": [ {{Rules(FactoryDefaultsFile.MaxRules + 1)}} ] }"""));
    }

    [Fact]
    public void ARuleWithTheSameSourceTwice_NamesTheSource() {
        FormatException exception = Assert.Throws<FormatException>(
            () => FactoryDefaultsFile.Parse("""{ "model": "A1843", "rules": [ { "source": "0x1000041", "target": "0x2" }, { "source": "0x01000041", "target": "0x3" } ] }"""));

        Assert.Contains("0x01000041", exception.Message);
    }

    [Fact]
    public void LoadingAFile_SaysWhichFileIsWrong() {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "model": "nope" }""");
        try {
            FormatException exception = Assert.Throws<FormatException>(() => FactoryDefaultsFile.Load(path));

            Assert.StartsWith(Path.GetFileName(path), exception.Message);
        } finally {
            File.Delete(path);
        }
    }
}
