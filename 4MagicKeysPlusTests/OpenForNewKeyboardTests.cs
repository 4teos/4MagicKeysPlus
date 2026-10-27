using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

public class OpenForNewKeyboardTests {
    private readonly FakeAppSettingsStore settings = new();

    private AgentOptionsViewModel Agent() {
        return new AgentOptionsViewModel(new FakeAgentLink { Reply = null }, new FakeAutostart(), settings, () => null);
    }

    [Fact]
    public async Task ByDefault_TheAppIsOpenedForANewKeyboard() {
        AgentOptionsViewModel agent = Agent();

        await agent.RefreshAsync();

        Assert.True(agent.OpenAppForNewKeyboard);
    }

    [Fact]
    public async Task TheSavedChoice_IsWhatTheScreenShows() {
        settings.Settings = new AppSettings(OpenAppForNewKeyboard: false);
        AgentOptionsViewModel agent = Agent();

        await agent.RefreshAsync();

        Assert.False(agent.OpenAppForNewKeyboard);
    }

    [Fact]
    public void TurningItOff_IsSaved_WithTheOtherSettingsAsTheyAre() {
        settings.Settings = new AppSettings(ThemeChoice.Dark, "ru");
        AgentOptionsViewModel agent = Agent();

        agent.SetOpenAppForNewKeyboard(false);

        Assert.Equal(new AppSettings(ThemeChoice.Dark, "ru", OpenAppForNewKeyboard: false), settings.Settings);
        Assert.False(agent.OpenAppForNewKeyboard);
        Assert.False(agent.HasOpenAppProblem);
    }

    [Fact]
    public void TurningItOnAgain_IsSaved() {
        settings.Settings = new AppSettings(OpenAppForNewKeyboard: false);
        AgentOptionsViewModel agent = Agent();

        agent.SetOpenAppForNewKeyboard(true);

        Assert.True(settings.Settings.OpenAppForNewKeyboard);
        Assert.True(agent.OpenAppForNewKeyboard);
    }

    [Fact]
    public void AChoiceThatCannotBeSaved_ShowsTheReason_AndWhatIsSaved() {
        settings.Failure = new UnauthorizedAccessException("denied");
        AgentOptionsViewModel agent = Agent();
        List<string?> changed = [];
        agent.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        agent.SetOpenAppForNewKeyboard(false);

        Assert.True(agent.OpenAppForNewKeyboard);
        Assert.True(agent.HasOpenAppProblem);
        Assert.Contains("denied", agent.OpenAppProblem);
        Assert.Contains(nameof(AgentOptionsViewModel.OpenAppForNewKeyboard), changed);
    }

    [Fact]
    public void AnotherSettingSavedOnTheAppearanceScreen_KeepsTheChoice() {
        settings.Settings = new AppSettings(ThemeChoice.System, null, OpenAppForNewKeyboard: false);
        FakeThemes themes = new();
        AppearanceViewModel appearance = new(settings, themes, [], new AppSettings(), () => null);

        appearance.ThemeIsDark = true;

        Assert.Equal(ThemeChoice.Dark, settings.Settings.Theme);
        Assert.False(settings.Settings.OpenAppForNewKeyboard);
    }

    [Fact]
    public async Task TheChoiceOfTheScreenIsNotUndoneByThePageThatWasOpenedBefore() {
        AppearanceViewModel appearance = new(settings, new FakeThemes(), [], new AppSettings(), () => null);
        AgentOptionsViewModel agent = Agent();

        agent.SetOpenAppForNewKeyboard(false);
        appearance.ThemeIsLight = true;
        await agent.RefreshAsync();

        Assert.False(settings.Settings.OpenAppForNewKeyboard);
        Assert.False(agent.OpenAppForNewKeyboard);
    }
}

[Collection(TestCollections.Registry)]
public sealed class AppSettingsOpenForNewKeyboardTests : IDisposable {
    private readonly ScratchKey scratch = new();
    private readonly RegistryAppSettingsStore store;

    public AppSettingsOpenForNewKeyboardTests() {
        store = new RegistryAppSettingsStore(Registry.CurrentUser, scratch.Path("Settings"));
    }

    public void Dispose() {
        scratch.Dispose();
    }

    [Fact]
    public void WithoutAKey_TheAppIsOpenedForANewKeyboard() {
        Assert.True(store.Read().OpenAppForNewKeyboard);
    }

    [Fact]
    public void TheChoice_IsKeptAsADword_TheAgentReads() {
        store.Write(new AppSettings(OpenAppForNewKeyboard: false));

        using RegistryKey key = scratch.Open("Settings")!;
        Assert.Equal(0, key.GetValue(AppSettingsRegistry.OpenAppForNewKeyboardValue));
        Assert.Equal(RegistryValueKind.DWord, key.GetValueKind(AppSettingsRegistry.OpenAppForNewKeyboardValue));
        Assert.False(store.Read().OpenAppForNewKeyboard);
        Assert.False(AppSettingsRegistry.ReadOpenAppForNewKeyboard(Registry.CurrentUser, scratch.Path("Settings")));
    }

    [Fact]
    public void TurningItOnAgain_IsKeptAsOne() {
        store.Write(new AppSettings(OpenAppForNewKeyboard: false));
        store.Write(new AppSettings(OpenAppForNewKeyboard: true));

        using RegistryKey key = scratch.Open("Settings")!;
        Assert.Equal(1, key.GetValue(AppSettingsRegistry.OpenAppForNewKeyboardValue));
        Assert.True(store.Read().OpenAppForNewKeyboard);
    }
}
