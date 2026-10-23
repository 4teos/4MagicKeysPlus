using System.Diagnostics;
using System.Xml.Linq;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

public class AgentVersionTests {
    [Fact]
    public void TheAgent_CarriesTheApplicationVersion() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "4MagicKeysPlus.sln"))) {
            directory = directory.Parent;
        }
        string props = File.ReadAllText(Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found"), "Directory.Build.props"));
        string appVersion = XDocument.Parse(props).Descendants("MkpAppVersion").Single().Value;

        FileVersionInfo info = FileVersionInfo.GetVersionInfo(typeof(Strings).Assembly.Location);

        Assert.Equal(appVersion, info.FileVersion);
        Assert.Equal(Version.Parse(appVersion), typeof(Strings).Assembly.GetName().Version);
        Assert.Equal("4MagicKeys+", info.ProductName);
    }
}
