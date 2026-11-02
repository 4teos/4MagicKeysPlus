using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

internal sealed record BatteryReading(BatteryTarget Target, KeyboardBatteryStatus? Status);

internal sealed record BatteryNotice(string Key, string? KeyboardName, int Percent);

// The thresholds are shared (BatteryAlertPolicy), but every keyboard has its own record of what it was warned about: warning one
// does not silence another, and charging one re-arms that one only.
internal sealed class BatteryAlerts {
    private readonly Dictionary<string, BatteryAlertPolicy> policies = [];

    public IReadOnlyList<BatteryNotice> Evaluate(IEnumerable<BatteryReading> readings) {
        List<BatteryNotice> due = [];
        foreach (BatteryReading reading in readings) {
            if (reading.Status is null) {
                continue;
            }

            if (!policies.TryGetValue(reading.Target.Key, out BatteryAlertPolicy? policy)) {
                policy = new BatteryAlertPolicy();
                policies[reading.Target.Key] = policy;
            }

            if (policy.Evaluate(reading.Status) is not null) {
                due.Add(new BatteryNotice(reading.Target.Key, reading.Target.Name, reading.Status.Percent));
            }
        }
        return due;
    }
}
