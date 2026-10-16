using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgentTests;

public class AgentProtocolTests {
    [Fact]
    public void AResponseSurvivesTheWire() {
        AgentResponse sent = AgentResponse.Success([new HotkeyRegistration("Hotkey_00_7F", 0), new HotkeyRegistration("Hotkey_02_41", 1409)]);

        AgentResponse? received = AgentProtocol.Deserialize(AgentProtocol.Serialize(sent));

        Assert.NotNull(received);
        Assert.True(received.Ok);
        Assert.Equal(sent.Hotkeys, received.Hotkeys);
        Assert.False(received.Hotkeys![1].Succeeded);
    }

    [Fact]
    public void AReplyIsASingleLine() {
        string line = AgentProtocol.Serialize(AgentResponse.Success([new HotkeyRegistration("a", 0)]));

        Assert.DoesNotContain('\n', line);
    }

    [Fact]
    public void AFailureCarriesItsMessage() {
        AgentResponse? received = AgentProtocol.Deserialize(AgentProtocol.Serialize(AgentResponse.Failure("nope")));

        Assert.NotNull(received);
        Assert.False(received.Ok);
        Assert.Equal("nope", received.Message);
        Assert.Null(received.Hotkeys);
    }

    [Theory]
    [InlineData(0x7Fu, 0u, "F16")]
    [InlineData(0x41u, 0x0002u | 0x0001u, "Ctrl+Alt+A")]
    [InlineData(0x54u, 0x0002u | 0x0004u | 0x0008u, "Ctrl+Shift+Win+T")]
    [InlineData(0x31u, 0u, "1")]
    [InlineData(0x65u, 0u, "Num 5")]
    [InlineData(0x2Cu, 0u, "Print Screen")]
    [InlineData(0xFFu, 0u, "VK 0xFF")]
    public void HotkeyText_ReadsLikeAShortcut(uint vk, uint modifiers, string expected) {
        Assert.Equal(expected, HotkeyText.Format(vk, modifiers));
    }
}
