namespace _4MagicKeysPlus.ViewModels;

// The Settings screen. Lives for the whole app session (owned by MainWindow), like the editors.
public sealed class OptionsViewModel {
    internal OptionsViewModel(AppearanceViewModel appearance, AgentOptionsViewModel agent, SystemStatusViewModel status, string appVersion) {
        Appearance = appearance;
        Agent = agent;
        Status = status;
        AppVersion = appVersion;
    }

    public AppearanceViewModel Appearance {
        get;
    }

    public AgentOptionsViewModel Agent {
        get;
    }

    public SystemStatusViewModel Status {
        get;
    }

    public string AppVersion {
        get;
    }

    public async Task RefreshAsync() {
        Status.Refresh();
        await Agent.RefreshAsync();
    }
}
