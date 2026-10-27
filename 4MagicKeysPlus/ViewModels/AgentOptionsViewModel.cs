using System.IO;
using System.Security;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

// Whether the agent runs, starting it, starting it at logon, and whether it opens this app for a new keyboard.
public sealed class AgentOptionsViewModel : ObservableObject {
    private readonly IAgentLink link;
    private readonly IAgentAutostart autostart;
    private readonly IAppSettingsStore settings;
    private readonly Func<string?> startAgent;
    private bool? running;
    private bool starting;
    private bool refreshing;
    private bool autostartEnabled;
    private bool openAppForNewKeyboard = true;
    private string? problem;
    private string? autostartProblem;
    private string? openAppProblem;

    // startAgent starts the agent and answers what went wrong, or null.
    internal AgentOptionsViewModel(IAgentLink link, IAgentAutostart autostart, IAppSettingsStore settings, Func<string?> startAgent) {
        this.link = link;
        this.autostart = autostart;
        this.settings = settings;
        this.startAgent = startAgent;
    }

    // Empty until the first answer, so that a running agent is not shown as stopped meanwhile.
    public string StatusText => starting ? Strings.OptionsAgentStarting : running switch {
        true => Strings.OptionsAgentRunning,
        false => Strings.OptionsAgentStopped,
        null => "",
    };

    public StatusLevel Level => starting ? StatusLevel.Warning : running switch {
        true => StatusLevel.Good,
        false => StatusLevel.Warning,
        null => StatusLevel.Neutral,
    };

    public bool CanStart => running == false && !starting;

    public string? Problem => problem;

    public bool HasProblem => problem is not null;

    public bool AutostartEnabled => autostartEnabled;

    public string? AutostartProblem => autostartProblem;

    public bool HasAutostartProblem => autostartProblem is not null;

    public bool OpenAppForNewKeyboard => openAppForNewKeyboard;

    public string? OpenAppProblem => openAppProblem;

    public bool HasOpenAppProblem => openAppProblem is not null;

    public async Task RefreshAsync() {
        if (refreshing || starting) {
            return;
        }

        refreshing = true;
        try {
            running = await link.IsRunningAsync();
            autostartEnabled = ReadAutostart();
            openAppForNewKeyboard = ReadOpenApp();
            NotifyAll();
        } finally {
            refreshing = false;
        }
    }

    public async Task StartAsync() {
        if (starting) {
            return;
        }

        starting = true;
        problem = startAgent();
        NotifyAll();
        try {
            bool answers = problem is null && await AgentLauncher.WaitUntilRunningAsync(link.IsRunningAsync);
            if (problem is null && !answers) {
                problem = Strings.OptionsAgentNoAnswer;
            }
            running = answers;
        } finally {
            starting = false;
        }
        NotifyAll();
    }

    public void SetAutostart(bool wanted) {
        autostartProblem = null;
        try {
            if (wanted) {
                autostartProblem = autostart.Enable();
            } else {
                autostart.Disable();
            }
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException) {
            autostartProblem = string.Format(Strings.OptionsAutostartFailed, exception.Message);
        }

        autostartEnabled = ReadAutostart();
        Notify(nameof(AutostartEnabled), nameof(AutostartProblem), nameof(HasAutostartProblem));
    }

    public void SetOpenAppForNewKeyboard(bool wanted) {
        openAppProblem = null;
        try {
            settings.Write(settings.Read() with {
                OpenAppForNewKeyboard = wanted
            });
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException) {
            openAppProblem = string.Format(Strings.OptionsSaveFailed, exception.Message);
        }

        openAppForNewKeyboard = ReadOpenApp();
        Notify(nameof(OpenAppForNewKeyboard), nameof(OpenAppProblem), nameof(HasOpenAppProblem));
    }

    // On is the default, so it is what a setting that cannot be read counts as.
    private bool ReadOpenApp() {
        try {
            return settings.Read().OpenAppForNewKeyboard;
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException) {
            return true;
        }
    }

    private bool ReadAutostart() {
        try {
            return autostart.IsEnabled();
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException) {
            return false;
        }
    }

    private void NotifyAll() {
        Notify(nameof(StatusText), nameof(Level), nameof(CanStart), nameof(Problem), nameof(HasProblem), nameof(AutostartEnabled), nameof(OpenAppForNewKeyboard));
    }
}
