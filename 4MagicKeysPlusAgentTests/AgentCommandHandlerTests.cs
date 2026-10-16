using _4MagicKeysPlus.Core;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

public class AgentCommandHandlerTests {
    private sealed class FakeRegistrar : IHotkeyRegistrar {
        public int Registered { get; private set; }

        public bool RegisterHotKey(HotkeyEntry entry, out int win32Error) {
            win32Error = 0;
            Registered++;
            return true;
        }

        public void UnregisterAll() {
            Registered = 0;
        }
    }

    private static (AgentCommandHandler Handler, FakeRegistrar Registrar) Create() {
        FakeRegistrar registrar = new();
        HotkeyManager manager = new(registrar, () => [new HotkeyEntry(1, "Hotkey_00_7F", "Calc", 0x7F, 0, @"C:\calc.exe")]);
        manager.Reload();
        return (new AgentCommandHandler(manager, work => work()), registrar);
    }

    [Fact]
    public void Pause_ReleasesTheHotkeys() {
        (AgentCommandHandler handler, FakeRegistrar registrar) = Create();

        AgentResponse response = handler.Handle(AgentProtocol.PauseCommand);

        Assert.True(response.Ok);
        Assert.Equal(0, registrar.Registered);
    }

    [Fact]
    public void Resume_TakesTheHotkeysBackAndReportsThem() {
        (AgentCommandHandler handler, FakeRegistrar registrar) = Create();
        handler.Handle(AgentProtocol.PauseCommand);

        AgentResponse response = handler.Handle(AgentProtocol.ResumeCommand);

        Assert.True(response.Ok);
        Assert.Equal(1, registrar.Registered);
        Assert.Equal("Hotkey_00_7F", Assert.Single(response.Hotkeys!).RegistryName);
    }

    [Fact]
    public void Reload_ReportsEveryHotkey() {
        (AgentCommandHandler handler, _) = Create();

        AgentResponse response = handler.Handle(AgentProtocol.ReloadCommand);

        Assert.True(response.Ok);
        Assert.True(Assert.Single(response.Hotkeys!).Succeeded);
    }

    [Fact]
    public void AnUnknownCommandIsRefused() {
        (AgentCommandHandler handler, _) = Create();

        AgentResponse response = handler.Handle("dance");

        Assert.False(response.Ok);
        Assert.Contains("dance", response.Message);
    }

    [Fact]
    public void AFailingCommandBecomesAFailureReply() {
        HotkeyManager manager = new(new FakeRegistrar(), () => throw new InvalidOperationException("boom"));
        AgentCommandHandler handler = new(manager, work => work());

        AgentResponse response = handler.Handle(AgentProtocol.ReloadCommand);

        Assert.False(response.Ok);
        Assert.Equal("boom", response.Message);
    }
}
