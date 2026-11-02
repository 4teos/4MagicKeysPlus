using System.Text.Json;
using System.Text.Json.Serialization;

namespace _4MagicKeysPlus.Core;

public sealed record HotkeyRegistration(string RegistryName, int Win32Error) {
    [JsonIgnore]
    public bool Succeeded => Win32Error == 0;
}

public sealed record AgentResponse(bool Ok, IReadOnlyList<HotkeyRegistration>? Hotkeys = null, string? Message = null) {
    public static AgentResponse Success(IReadOnlyList<HotkeyRegistration>? hotkeys = null) {
        return new AgentResponse(true, hotkeys);
    }

    public static AgentResponse Failure(string message) {
        return new AgentResponse(false, null, message);
    }
}

// One command per line, one JSON reply per line back. "pause" releases the hotkeys so that a key being captured reaches the app;
// it ends with "resume", with "reload" (which also re-reads the configuration) or when the connection that paused it drops.
public static class AgentProtocol {
    public const string PipeName = "4MagicKeysPlus.Agent";
    public const string PauseCommand = "pause";
    public const string ResumeCommand = "resume";
    public const string ReloadCommand = "reload";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(AgentResponse response) {
        return JsonSerializer.Serialize(response, Options);
    }

    public static AgentResponse? Deserialize(string line) {
        return JsonSerializer.Deserialize<AgentResponse>(line, Options);
    }
}
