using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

internal sealed class FakeAppSettingsStore : IAppSettingsStore {
    public AppSettings Settings { get; set; } = new();
    public Exception? Failure {
        get; set;
    }
    public int Writes {
        get; private set;
    }

    public AppSettings Read() {
        return Settings;
    }

    public void Write(AppSettings settings) {
        if (Failure is not null) {
            throw Failure;
        }
        Writes++;
        Settings = settings;
    }
}

internal sealed class FakeThemes : IThemeApplier {
    public List<ThemeChoice> Applied { get; } = [];

    public void Apply(ThemeChoice choice) {
        Applied.Add(choice);
    }
}

internal sealed class FakeAutostart : IAgentAutostart {
    public bool Enabled {
        get; set;
    }
    public string? EnableProblem {
        get; set;
    }
    public Exception? Failure {
        get; set;
    }

    public bool IsEnabled() {
        return Enabled;
    }

    public string? Enable() {
        if (Failure is not null) {
            throw Failure;
        }
        Enabled = EnableProblem is null;
        return EnableProblem;
    }

    public void Disable() {
        if (Failure is not null) {
            throw Failure;
        }
        Enabled = false;
    }
}

internal sealed class FakeDriverProbe : IDriverProbe {
    public DriverState State { get; set; } = DriverState.Active;
    public string? Version { get; set; } = "2.2.0.34";

    public DriverState Probe() {
        return State;
    }

    public string? InstalledVersion() {
        return Version;
    }
}

public class AppearanceViewModelTests {
    private static readonly LanguageOption[] Available = [new("en", "English"), new("ru", "Русский")];

    private readonly FakeAppSettingsStore store = new();
    private readonly FakeThemes themes = new();
    private int restarts;
    private string? restartProblem;

    private AppearanceViewModel Appearance(AppSettings? launched = null) {
        return new AppearanceViewModel(store, themes, Available, launched ?? new AppSettings(), () => {
            restarts++;
            return restartProblem;
        });
    }

    [Fact]
    public void TheThemeThatWasLaunchedWith_IsTheSelectedOne() {
        AppearanceViewModel appearance = Appearance(new AppSettings(ThemeChoice.Dark));

        Assert.True(appearance.ThemeIsDark);
        Assert.False(appearance.ThemeIsSystem);
        Assert.False(appearance.ThemeIsLight);
    }

    [Fact]
    public void ChoosingATheme_SavesAndAppliesIt() {
        AppearanceViewModel appearance = Appearance();

        appearance.ThemeIsLight = true;

        Assert.Equal(ThemeChoice.Light, store.Settings.Theme);
        Assert.Equal([ThemeChoice.Light], themes.Applied);
        Assert.True(appearance.ThemeIsLight);
        Assert.False(appearance.ThemeIsSystem);
    }

    [Fact]
    public void ChoosingATheme_ThatIsSelected_ChangesNothing() {
        AppearanceViewModel appearance = Appearance(new AppSettings(ThemeChoice.Dark));

        appearance.ThemeIsDark = true;
        appearance.ThemeIsLight = false;

        Assert.Equal(0, store.Writes);
        Assert.Empty(themes.Applied);
    }

    [Fact]
    public void WhenSavingFails_TheThemeIsNotApplied_AndTheScreenSaysWhy() {
        store.Failure = new UnauthorizedAccessException("denied");
        AppearanceViewModel appearance = Appearance();
        List<string?> notified = [];
        appearance.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        appearance.ThemeIsDark = true;

        Assert.Empty(themes.Applied);
        Assert.True(appearance.ThemeIsSystem);
        Assert.True(appearance.HasProblem);
        Assert.Contains("denied", appearance.Problem);
        Assert.Contains(nameof(AppearanceViewModel.ThemeIsDark), notified);
    }

    [Fact]
    public void ASuccessfulSave_ClearsTheProblem() {
        store.Failure = new UnauthorizedAccessException("denied");
        AppearanceViewModel appearance = Appearance();
        appearance.ThemeIsDark = true;
        store.Failure = null;

        appearance.ThemeIsDark = true;

        Assert.False(appearance.HasProblem);
    }

    [Fact]
    public void TheLanguages_AreSystemFirstThenTheInstalledOnes() {
        AppearanceViewModel appearance = Appearance();

        Assert.Equal([null, "en", "ru"], appearance.Languages.Select(language => language.CultureName));
        Assert.Null(appearance.SelectedLanguage.CultureName);
    }

    [Fact]
    public void TheLanguageThatWasLaunchedWith_IsTheSelectedOne() {
        AppearanceViewModel appearance = Appearance(new AppSettings(Language: "ru"));

        Assert.Equal("ru", appearance.SelectedLanguage.CultureName);
        Assert.False(appearance.LanguageRestartRequired);
    }

    [Fact]
    public void ALanguageThatIsNotInstalled_IsShownAsSystem() {
        AppearanceViewModel appearance = Appearance(new AppSettings(Language: "de"));

        Assert.Null(appearance.SelectedLanguage.CultureName);
        Assert.False(appearance.LanguageRestartRequired);
    }

    [Fact]
    public void ChoosingALanguage_SavesIt_AndAsksForARestart() {
        AppearanceViewModel appearance = Appearance();

        appearance.SelectedLanguage = Available[1];

        Assert.Equal("ru", store.Settings.Language);
        Assert.True(appearance.LanguageRestartRequired);
        Assert.Empty(themes.Applied);
    }

    [Fact]
    public void ChoosingTheLanguageThatIsRunning_NeedsNoRestart() {
        AppearanceViewModel appearance = Appearance(new AppSettings(Language: "ru"));
        appearance.SelectedLanguage = Available[0];
        Assert.True(appearance.LanguageRestartRequired);

        appearance.SelectedLanguage = Available[1];

        Assert.False(appearance.LanguageRestartRequired);
    }

    [Fact]
    public void ChoosingSystem_SavesNoLanguage() {
        store.Settings = new AppSettings(Language: "ru");
        AppearanceViewModel appearance = Appearance(store.Settings);

        appearance.SelectedLanguage = appearance.Languages[0];

        Assert.Null(store.Settings.Language);
        Assert.True(appearance.LanguageRestartRequired);
    }

    [Fact]
    public void ALanguageChange_KeepsTheTheme() {
        AppearanceViewModel appearance = Appearance(new AppSettings(ThemeChoice.Dark));

        appearance.SelectedLanguage = Available[1];

        Assert.Equal(new AppSettings(ThemeChoice.Dark, "ru"), store.Settings);
    }

    [Fact]
    public void Restart_AsksTheAppToRestart() {
        AppearanceViewModel appearance = Appearance();

        appearance.Restart();

        Assert.Equal(1, restarts);
        Assert.False(appearance.HasProblem);
    }

    [Fact]
    public void WhenTheRestartFails_TheScreenSaysWhy() {
        restartProblem = "no way";
        AppearanceViewModel appearance = Appearance();

        appearance.Restart();

        Assert.Equal("no way", appearance.Problem);
    }
}

public class AgentOptionsViewModelTests {
    private readonly FakeAgentLink link = new() {
        Reply = null
    };
    private readonly FakeAutostart autostart = new();
    private readonly FakeAppSettingsStore settings = new();
    private string? startProblem;
    private Action onStart = () => { };

    private AgentOptionsViewModel Agent() {
        return new AgentOptionsViewModel(link, autostart, settings, () => {
            onStart();
            return startProblem;
        });
    }

    [Fact]
    public void BeforeTheFirstAnswer_NothingIsClaimed() {
        AgentOptionsViewModel agent = Agent();

        Assert.Equal("", agent.StatusText);
        Assert.Equal(StatusLevel.Neutral, agent.Level);
        Assert.False(agent.CanStart);
    }

    [Fact]
    public async Task ARunningAgent_IsGoodAndNeedsNoStart() {
        link.Reply = AgentResponse.Success([]);
        AgentOptionsViewModel agent = Agent();

        await agent.RefreshAsync();

        Assert.Equal(StatusLevel.Good, agent.Level);
        Assert.NotEqual("", agent.StatusText);
        Assert.False(agent.CanStart);
    }

    [Fact]
    public async Task AStoppedAgent_CanBeStarted() {
        AgentOptionsViewModel agent = Agent();

        await agent.RefreshAsync();

        Assert.Equal(StatusLevel.Warning, agent.Level);
        Assert.True(agent.CanStart);
    }

    [Fact]
    public async Task Starting_WaitsForTheAgentToAnswer() {
        onStart = () => link.Reply = AgentResponse.Success([]);
        AgentOptionsViewModel agent = Agent();
        await agent.RefreshAsync();

        await agent.StartAsync();

        Assert.Equal(StatusLevel.Good, agent.Level);
        Assert.False(agent.CanStart);
        Assert.False(agent.HasProblem);
    }

    [Fact]
    public async Task AnAgentThatCannotBeStarted_ShowsWhy() {
        startProblem = "no file";
        AgentOptionsViewModel agent = Agent();
        await agent.RefreshAsync();

        await agent.StartAsync();

        Assert.Equal("no file", agent.Problem);
        Assert.True(agent.CanStart);
    }

    [Fact]
    public async Task TheAutostart_IsReadFromWhatIsReallySet() {
        autostart.Enabled = true;
        AgentOptionsViewModel agent = Agent();
        Assert.False(agent.AutostartEnabled);

        await agent.RefreshAsync();

        Assert.True(agent.AutostartEnabled);
    }

    [Fact]
    public void TurningTheAutostartOn_AndOff() {
        AgentOptionsViewModel agent = Agent();

        agent.SetAutostart(true);
        Assert.True(agent.AutostartEnabled);
        Assert.True(autostart.Enabled);

        agent.SetAutostart(false);
        Assert.False(agent.AutostartEnabled);
        Assert.False(autostart.Enabled);
    }

    [Fact]
    public void WhenItCannotBeTurnedOn_TheSwitchStaysOff_AndSaysWhy() {
        autostart.EnableProblem = "no agent";
        AgentOptionsViewModel agent = Agent();

        agent.SetAutostart(true);

        Assert.False(agent.AutostartEnabled);
        Assert.Equal("no agent", agent.AutostartProblem);
    }

    [Fact]
    public void WhenTheRegistryRefuses_TheSwitchSaysWhy() {
        autostart.Failure = new UnauthorizedAccessException("denied");
        AgentOptionsViewModel agent = Agent();

        agent.SetAutostart(true);

        Assert.False(agent.AutostartEnabled);
        Assert.Contains("denied", agent.AutostartProblem);
    }

    [Fact]
    public void ASuccessfulChange_ClearsTheProblem() {
        autostart.EnableProblem = "no agent";
        AgentOptionsViewModel agent = Agent();
        agent.SetAutostart(true);
        autostart.EnableProblem = null;

        agent.SetAutostart(true);

        Assert.False(agent.HasAutostartProblem);
    }

    [Fact]
    public async Task WaitingForTheAgent_GivesUpAfterTheLastPoll() {
        int asked = 0;

        bool answered = await AgentLauncher.WaitUntilRunningAsync(() => {
            asked++;
            return Task.FromResult(false);
        }, polls: 3, interval: TimeSpan.FromMilliseconds(1));

        Assert.False(answered);
        Assert.Equal(3, asked);
    }

    [Fact]
    public async Task WaitingForTheAgent_StopsAtTheFirstAnswer() {
        int asked = 0;

        bool answered = await AgentLauncher.WaitUntilRunningAsync(() => Task.FromResult(++asked == 2), polls: 5, interval: TimeSpan.FromMilliseconds(1));

        Assert.True(answered);
        Assert.Equal(2, asked);
    }
}

public class SystemStatusViewModelTests {
    private readonly FakeDriverProbe driver = new();
    private string? keyboard;

    private SystemStatusViewModel Status() {
        return new SystemStatusViewModel(driver, () => keyboard);
    }

    [Theory]
    [InlineData(DriverState.Active, StatusLevel.Good)]
    [InlineData(DriverState.Idle, StatusLevel.Neutral)]
    [InlineData(DriverState.NotInstalled, StatusLevel.Bad)]
    public void TheDriver_IsShownAccordingToItsState(DriverState state, StatusLevel expected) {
        driver.State = state;

        SystemStatusViewModel status = Status();

        Assert.Equal(expected, status.DriverLevel);
        Assert.NotEqual("", status.DriverText);
    }

    [Fact]
    public void AConnectedKeyboard_IsNamed() {
        keyboard = "Magic Keyboard, Bluetooth";

        SystemStatusViewModel status = Status();

        Assert.Equal("Magic Keyboard, Bluetooth", status.KeyboardText);
        Assert.Equal(StatusLevel.Good, status.KeyboardLevel);
    }

    [Fact]
    public void WithoutAKeyboard_ItIsNeutral() {
        SystemStatusViewModel status = Status();

        Assert.NotEqual("", status.KeyboardText);
        Assert.Equal(StatusLevel.Neutral, status.KeyboardLevel);
    }

    [Fact]
    public void TheVersionOfTheInstalledDriver_IsShown() {
        driver.Version = "2.2.0.34";

        Assert.Equal("2.2.0.34", Status().DriverVersion);
    }

    [Fact]
    public void WithoutAVersion_ADashIsShown() {
        driver.Version = null;

        Assert.Equal("—", Status().DriverVersion);
    }

    [Fact]
    public void ADriverInstalledWhileTheScreenIsOpen_ShowsItsVersionAtTheNextRefresh() {
        driver.Version = null;
        SystemStatusViewModel status = Status();
        List<string?> notified = [];
        status.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        driver.Version = "2.3.0.1";
        status.Refresh();

        Assert.Equal("2.3.0.1", status.DriverVersion);
        Assert.Contains(nameof(SystemStatusViewModel.DriverVersion), notified);
    }

    [Fact]
    public void Refreshing_FollowsTheDriverAndTheKeyboard() {
        driver.State = DriverState.Idle;
        SystemStatusViewModel status = Status();
        List<string?> notified = [];
        status.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        driver.State = DriverState.Active;
        keyboard = "Magic Keyboard, USB";
        status.Refresh();

        Assert.Equal(StatusLevel.Good, status.DriverLevel);
        Assert.Equal("Magic Keyboard, USB", status.KeyboardText);
        Assert.Contains(nameof(SystemStatusViewModel.DriverLevel), notified);
    }
}

public class OptionsViewModelTests {
    [Fact]
    public void TheApplicationVersion_IsWhatItWasGiven() {
        OptionsViewModel options = new(
            new AppearanceViewModel(new FakeAppSettingsStore(), new FakeThemes(), [], new AppSettings(), () => null),
            new AgentOptionsViewModel(new FakeAgentLink(), new FakeAutostart(), new FakeAppSettingsStore(), () => null),
            new SystemStatusViewModel(new FakeDriverProbe(), () => null),
            "2.3.0.14");

        Assert.Equal("2.3.0.14", options.AppVersion);
    }

    [Fact]
    public async Task Refreshing_AsksTheDriverAndTheAgentAgain() {
        FakeDriverProbe driver = new() {
            State = DriverState.Idle
        };
        FakeAgentLink link = new() {
            Reply = null
        };
        OptionsViewModel options = new(
            new AppearanceViewModel(new FakeAppSettingsStore(), new FakeThemes(), [], new AppSettings(), () => null),
            new AgentOptionsViewModel(link, new FakeAutostart(), new FakeAppSettingsStore(), () => null),
            new SystemStatusViewModel(driver, () => null),
            "2.3.0.14");
        await options.RefreshAsync();
        Assert.Equal(StatusLevel.Neutral, options.Status.DriverLevel);
        Assert.True(options.Agent.CanStart);

        driver.State = DriverState.Active;
        link.Reply = AgentResponse.Success([]);
        await options.RefreshAsync();

        Assert.Equal(StatusLevel.Good, options.Status.DriverLevel);
        Assert.Equal(StatusLevel.Good, options.Agent.Level);
    }
}
