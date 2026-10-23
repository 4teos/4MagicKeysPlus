using System.Diagnostics;
using System.Xml.Linq;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Properties;

namespace _4MagicKeysPlusTests;

// There are two versions: of the application (the app, the agent and the libraries) and of the
// driver. These check the places they are declared in, and that the build carries them.
public class VersionTests {
    private static string Root {
        get {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "4MagicKeysPlus.sln"))) {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found");
        }
    }

    private static string Read(string relativePath) {
        return File.ReadAllText(Path.Combine(Root, relativePath));
    }

    private static string Property(XDocument document, string name) {
        return document.Descendants(name).Single().Value;
    }

    private static string AppVersion => Property(XDocument.Parse(Read("Directory.Build.props")), "MkpAppVersion");

    [Fact]
    public void TheApplicationVersion_IsDeclaredOnce_AsFourParts() {
        XDocument props = XDocument.Parse(Read("Directory.Build.props"));

        Assert.Matches(@"^\d+(\.\d+){3}$", AppVersion);
        Assert.Equal("$(MkpAppVersion)", Property(props, "Version"));
        Assert.Equal("4teos", Property(props, "Company"));
        Assert.Equal("4MagicKeys+", Property(props, "Product"));
        Assert.Contains("4teos", Property(props, "Copyright"));
    }

    [Theory]
    [InlineData(@"4MagicKeysPlus\4MagicKeysPlus.csproj")]
    [InlineData(@"4MagicKeysPlusAgent\4MagicKeysPlusAgent.csproj")]
    [InlineData(@"4MagicKeysPlusCore\4MagicKeysPlusCore.csproj")]
    public void AProjectOfTheApplication_DoesNotDeclareAVersionOfItsOwn(string project) {
        XDocument document = XDocument.Parse(Read(project));

        foreach (string name in new[] { "Version", "AssemblyVersion", "FileVersion", "Company", "Product", "Copyright" }) {
            Assert.Empty(document.Descendants(name));
        }
        Assert.StartsWith("4MagicKeys+ ", Property(document, "AssemblyTitle"));
    }

    [Fact]
    public void TheBuiltAssemblies_CarryTheApplicationVersion() {
        foreach (System.Reflection.Assembly assembly in new[] { typeof(Strings).Assembly, typeof(HotkeyText).Assembly }) {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(assembly.Location);

            Assert.Equal(Version.Parse(AppVersion), assembly.GetName().Version);
            Assert.Equal(AppVersion, info.FileVersion);
            Assert.Equal("4MagicKeys+", info.ProductName);
            Assert.Equal("4teos", info.CompanyName);
        }
    }

    [Fact]
    public void TheDriver_HasAFourPartVersionInItsInf() {
        Assert.Matches(@"(?m)^DriverVer\s*=\s*\d+/\d+/\d{4}\s*,\s*\d+(\.\d+){3}\s*$", Read(@"4MagicKeysPlusDriver\4MagicKeysPlus.inf"));
    }

    [Fact]
    public void TheVersionOfTheDriverFile_IsTakenFromTheInf_NotWrittenTwice() {
        string resource = Read(@"4MagicKeysPlusDriver\Driver.rc");
        Assert.DoesNotMatch(@"\d+(\.|,\s*)\d+(\.|,\s*)\d+(\.|,\s*)\d+", resource);
        Assert.Contains("MKP_VERSION_NUM", resource);
        Assert.Contains("MkpDriverVersion", Read(@"4MagicKeysPlusDriver\4MagicKeysPlusDriver.vcxproj"));
    }
}
