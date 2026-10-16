using _4MagicKeysPlus.Core;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

public class AgentPipeServerTests : IDisposable {
    private sealed class FakeRegistrar : IHotkeyRegistrar {
        private int registered;

        public int Registered => Volatile.Read(ref registered);

        public bool RegisterHotKey(HotkeyEntry entry, out int win32Error) {
            win32Error = 0;
            Interlocked.Increment(ref registered);
            return true;
        }

        public void UnregisterAll() {
            Volatile.Write(ref registered, 0);
        }
    }

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly string pipeName = $"4MagicKeysPlus.Agent.Test.{Guid.NewGuid():N}";
    private readonly FakeRegistrar registrar = new();
    private readonly List<IDisposable> disposables = [];

    public void Dispose() {
        foreach (IDisposable disposable in disposables) {
            disposable.Dispose();
        }
    }

    private void StartServer(TimeSpan pauseTimeout) {
        HotkeyManager manager = new(registrar, () => [new HotkeyEntry(1, "Hotkey_00_7F", "Calc", 0x7F, 0, @"C:\calc.exe")]);
        manager.Reload();
        AgentPipeServer server = new(new AgentCommandHandler(manager, work => work()), pauseTimeout, pipeName);
        server.Start();
        disposables.Add(server);
    }

    private async Task<AgentConnection> Connect() {
        AgentConnection? connection = await AgentConnection.ConnectAsync(ConnectTimeout, pipeName);
        Assert.NotNull(connection);
        disposables.Add(connection);
        return connection;
    }

    private static async Task WaitUntil(Func<bool> condition) {
        for (int i = 0; i < 200 && !condition(); i++) {
            await Task.Delay(25);
        }
        Assert.True(condition());
    }

    [Fact]
    public async Task Reload_RoundTripsThroughThePipe() {
        StartServer(TimeSpan.FromSeconds(30));
        AgentConnection connection = await Connect();

        AgentResponse response = await connection.SendAsync(AgentProtocol.ReloadCommand);

        Assert.True(response.Ok);
        Assert.Equal("Hotkey_00_7F", Assert.Single(response.Hotkeys!).RegistryName);
    }

    [Fact]
    public async Task Pause_IsAcknowledgedOnlyOnceTheHotkeysAreReleased() {
        StartServer(TimeSpan.FromSeconds(30));
        AgentConnection connection = await Connect();

        AgentResponse response = await connection.SendAsync(AgentProtocol.PauseCommand);

        Assert.True(response.Ok);
        Assert.Equal(0, registrar.Registered);
    }

    [Fact]
    public async Task ADroppedConnectionEndsItsPause() {
        StartServer(TimeSpan.FromSeconds(30));
        AgentConnection connection = await Connect();
        await connection.SendAsync(AgentProtocol.PauseCommand);

        connection.Dispose();

        await WaitUntil(() => registrar.Registered == 1);
    }

    [Fact]
    public async Task APauseThatIsNeverEndedTimesOut() {
        StartServer(TimeSpan.FromMilliseconds(200));
        AgentConnection connection = await Connect();
        await connection.SendAsync(AgentProtocol.PauseCommand);

        await WaitUntil(() => registrar.Registered == 1);
    }

    [Fact]
    public async Task Reload_EndsThePause() {
        StartServer(TimeSpan.FromSeconds(30));
        AgentConnection connection = await Connect();
        await connection.SendAsync(AgentProtocol.PauseCommand);

        await connection.SendAsync(AgentProtocol.ReloadCommand);

        Assert.Equal(1, registrar.Registered);
    }

    [Fact]
    public async Task NoServer_MeansNoConnection() {
        AgentConnection? connection = await AgentConnection.ConnectAsync(TimeSpan.FromMilliseconds(200), pipeName);

        Assert.Null(connection);
    }
}
