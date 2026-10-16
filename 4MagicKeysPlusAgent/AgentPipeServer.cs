using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

// Serves the agent's pipe (see AgentProtocol). A connection that pauses the hotkeys owns the
// pause: when it drops, or sends nothing for PauseTimeout, the hotkeys come back.
internal sealed class AgentPipeServer : IDisposable {
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly AgentCommandHandler handler;
    private readonly TimeSpan pauseTimeout;
    private readonly string pipeName;
    private readonly CancellationTokenSource stopping = new();

    public AgentPipeServer(AgentCommandHandler handler, TimeSpan pauseTimeout, string pipeName = AgentProtocol.PipeName) {
        this.handler = handler;
        this.pauseTimeout = pauseTimeout;
        this.pipeName = pipeName;
    }

    public void Start() {
        _ = Task.Run(AcceptLoop);
    }

    public void Dispose() {
        stopping.Cancel();
        stopping.Dispose();
    }

    private async Task AcceptLoop() {
        try {
            while (!stopping.IsCancellationRequested) {
                NamedPipeServerStream pipe = CreatePipe(pipeName);
                try {
                    await pipe.WaitForConnectionAsync(stopping.Token);
                } catch (Exception ex) when (ex is not OperationCanceledException) {
                    Logger.Error($"Pipe accept failed: {ex.Message}");
                    await pipe.DisposeAsync();
                    await Task.Delay(TimeSpan.FromSeconds(1), stopping.Token);
                    continue;
                } catch {
                    await pipe.DisposeAsync();
                    throw;
                }

                _ = Task.Run(() => Serve(pipe));
            }
        } catch (OperationCanceledException) {
            // The agent is shutting down.
        }
    }

    private async Task Serve(NamedPipeServerStream pipe) {
        bool paused = false;
        try {
            using (pipe) {
                using StreamReader reader = new(pipe, Utf8, false, 1024, leaveOpen: true);
                using StreamWriter writer = new(pipe, Utf8, 1024, leaveOpen: true) { AutoFlush = true };
                while (true) {
                    using CancellationTokenSource readTimeout = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                    if (paused) {
                        readTimeout.CancelAfter(pauseTimeout);
                    }

                    string? line = await reader.ReadLineAsync(readTimeout.Token);
                    if (line is null) {
                        break;
                    }

                    string command = line.Trim();
                    AgentResponse response = handler.Handle(command);
                    if (response.Ok && command == AgentProtocol.PauseCommand) {
                        paused = true;
                    } else if (response.Ok && command is AgentProtocol.ResumeCommand or AgentProtocol.ReloadCommand) {
                        paused = false;
                    }
                    await writer.WriteLineAsync(AgentProtocol.Serialize(response));
                }
            }
        } catch (OperationCanceledException) {
            if (paused) {
                Logger.Warn("A paused connection sent nothing in time, resuming the hotkeys.");
            }
        } catch (IOException) {
            // The client went away; the finally below puts the hotkeys back.
        } catch (Exception ex) {
            Logger.Error($"Pipe connection failed: {ex.Message}");
        } finally {
            if (paused && !stopping.IsCancellationRequested) {
                handler.Handle(AgentProtocol.ResumeCommand);
            }
        }
    }

    // The configuration app runs elevated, so Administrators get in as well as the user.
    private static NamedPipeServerStream CreatePipe(string pipeName) {
        PipeSecurity security = new();
        security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
    }
}
