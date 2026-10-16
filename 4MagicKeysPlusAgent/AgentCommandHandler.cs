using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

// Turns a command from the pipe into a call on the hotkeys. The pipe is served from thread-pool
// threads, so runOnUiThread must hand the work to the thread that owns the hotkey window.
internal sealed class AgentCommandHandler {
    private readonly HotkeyManager hotkeys;
    private readonly Func<Func<AgentResponse>, AgentResponse> runOnUiThread;

    public AgentCommandHandler(HotkeyManager hotkeys, Func<Func<AgentResponse>, AgentResponse> runOnUiThread) {
        this.hotkeys = hotkeys;
        this.runOnUiThread = runOnUiThread;
    }

    public AgentResponse Handle(string command) {
        try {
            return command switch {
                AgentProtocol.PauseCommand => runOnUiThread(() => {
                    hotkeys.Pause();
                    return AgentResponse.Success();
                }),
                AgentProtocol.ResumeCommand => runOnUiThread(() => AgentResponse.Success(hotkeys.Resume())),
                AgentProtocol.ReloadCommand => runOnUiThread(() => AgentResponse.Success(hotkeys.Reload())),
                _ => AgentResponse.Failure($"Unknown command '{command}'."),
            };
        } catch (Exception ex) {
            Logger.Error($"Command '{command}' failed: {ex.Message}");
            return AgentResponse.Failure(ex.Message);
        }
    }
}
