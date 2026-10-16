using System.IO.Pipes;
using System.Text;

namespace _4MagicKeysPlus.Core;

// A client connection to the agent's pipe. Keep it open while the agent is paused: closing it
// is what tells the agent that nobody is capturing a hotkey any more.
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

    // Null when the agent does not answer in time, which means it is not running.
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

    // The reader and writer own nothing but the pipe, and the writer flushes after every line.
    public void Dispose() {
        pipe.Dispose();
    }
}
