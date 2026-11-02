using System.IO.Pipes;
using System.Text;

namespace _4MagicKeysPlus.Core;

// Keep it open while the agent is paused: closing it is what resumes the agent's hotkeys.
public sealed class AgentConnection : IDisposable {
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly NamedPipeClientStream pipe;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;

    private AgentConnection(NamedPipeClientStream pipe) {
        this.pipe = pipe;
        reader = new StreamReader(pipe, Utf8, false, 1024, leaveOpen: true);
        writer = new StreamWriter(pipe, Utf8, 1024, leaveOpen: true) { AutoFlush = true };
    }

    public static async Task<AgentConnection?> ConnectAsync(TimeSpan timeout, string pipeName = AgentProtocol.PipeName) {
        NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try {
            await pipe.ConnectAsync((int)timeout.TotalMilliseconds);
            return new AgentConnection(pipe);
        } catch (Exception ex) when (ex is TimeoutException or IOException) {
            await pipe.DisposeAsync();
            return null;
        }
    }

    public async Task<AgentResponse> SendAsync(string command, CancellationToken cancellationToken = default) {
        await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
        string? line = await reader.ReadLineAsync(cancellationToken);
        if (line is null) {
            throw new IOException("The agent closed the connection.");
        }
        return AgentProtocol.Deserialize(line) ?? throw new IOException("The agent sent an empty reply.");
    }

    public void Dispose() {
        pipe.Dispose();
    }
}
