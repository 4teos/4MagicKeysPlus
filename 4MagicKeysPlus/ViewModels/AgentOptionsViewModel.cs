using System.IO;
using System.Security;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

// Whether the agent runs, starting it, and starting it at logon.
public sealed class AgentOptionsViewModel : ObservableObject {
    private readonly IAgentLink link;
    private readonly IAgentAutostart autostart;
    private readonly Func<string?> startAgent;
    private bool? running;
    private bool starting;
    private bool refreshing;
    private bool autostartEnabled;
    private string? problem;
    private string? autostartProblem;

    // startAgent starts the agent and answers what went wrong, or null.
    internal AgentOptionsViewModel(IAgentLink link, IAgentAutostart autostart, Func<string?> startAgent) {
        this.link = link;
        this.autostart = autostart;
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

    public async Task RefreshAsync() {
        if (refreshing || starting) {
            return;
        }

        refreshing = true;
        try {
            running = await link.IsRunningAsync();
            autostartEnabled = ReadAutostart();
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

    private bool ReadAutostart() {
        try {
            return autostart.IsEnabled();
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException) {
            return false;
        }
    }

    private void NotifyAll() {
        Notify(nameof(StatusText), nameof(Level), nameof(CanStart), nameof(Problem), nameof(HasProblem), nameof(AutostartEnabled));
    }
}
