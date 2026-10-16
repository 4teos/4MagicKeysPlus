using System.IO;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlus.Services;

// While held, the agent's hotkeys are released so that a key press being captured reaches this app.
internal interface IAgentPause : IDisposable {
    Task ResumeAsync();
}

// The configuration app's side of the agent's pipe. Every call answers null when the agent is
// not running.
internal interface IAgentLink {
    Task<AgentResponse?> ReloadAsync();

    Task<IAgentPause?> PauseAsync();
}

internal sealed class AgentLink : IAgentLink {
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(500);

    private readonly string pipeName;

    public AgentLink(string pipeName = AgentProtocol.PipeName) {
        this.pipeName = pipeName;
    }

    public async Task<AgentResponse?> ReloadAsync() {
        using AgentConnection? connection = await AgentConnection.ConnectAsync(ConnectTimeout, pipeName);
        if (connection is null) {
            return null;
        }
        try {
            return await connection.SendAsync(AgentProtocol.ReloadCommand);
        } catch (IOException) {
            return null;
        }
    }

    public async Task<IAgentPause?> PauseAsync() {
        AgentConnection? connection = await AgentConnection.ConnectAsync(ConnectTimeout, pipeName);
        if (connection is null) {
            return null;
        }
        try {
            await connection.SendAsync(AgentProtocol.PauseCommand);
            return new Pause(connection);
        } catch (IOException) {
            connection.Dispose();
            return null;
        }
    }

    // Closing the connection is enough to end the pause, the agent resumes by itself. Resuming
    // explicitly just does it before the pipe is torn down.
    private sealed class Pause(AgentConnection connection) : IAgentPause {
        public async Task ResumeAsync() {
            try {
                await connection.SendAsync(AgentProtocol.ResumeCommand);
            } catch (IOException) {
                // The agent is gone, so there is nothing left to resume.
            }
        }

        public void Dispose() {
            connection.Dispose();
        }
    }
}
