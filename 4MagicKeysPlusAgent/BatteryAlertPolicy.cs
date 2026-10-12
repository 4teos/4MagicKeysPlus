using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

// Decides when a low-battery notice is due. The two thresholds are independent one-shot
// flags kept in memory only; charging re-arms them, so a keyboard that was recharged and
// ran down again later in the same logon warns again.
internal sealed class BatteryAlertPolicy {
    public const int WarningPercent = 20;
    public const int CriticalPercent = 5;

    private bool notifiedWarning;
    private bool notifiedCritical;

    // Returns the threshold to warn about, or null when there is nothing to say.
    // Both thresholds crossed at once yield the critical one only.
    public int? Evaluate(KeyboardBatteryStatus status) {
        if (status.IsCharging) {
            notifiedWarning = false;
            notifiedCritical = false;
            return null;
        }

        int? threshold = null;
        if (status.Percent <= CriticalPercent && !notifiedCritical) {
            notifiedCritical = true;
            threshold = CriticalPercent;
        }

        if (status.Percent <= WarningPercent && !notifiedWarning) {
            notifiedWarning = true;
            threshold ??= WarningPercent;
        }

        return threshold;
    }
}
