using System.Globalization;
using System.Resources;

namespace _4MagicKeysPlus.Properties;

public static class Strings {
    private static readonly ResourceManager ResourceManager =
        new("_4MagicKeysPlus.Properties.Strings", typeof(Strings).Assembly);

    public static string NavKeys {
        get {
            return Get(nameof(NavKeys));
        }
    }

    public static string NavQuickLaunch {
        get {
            return Get(nameof(NavQuickLaunch));
        }
    }

    public static string NavSettings {
        get {
            return Get(nameof(NavSettings));
        }
    }

    public static string DeviceNotDetected {
        get {
            return Get(nameof(DeviceNotDetected));
        }
    }

    public static string ConnectionUsb {
        get {
            return Get(nameof(ConnectionUsb));
        }
    }

    public static string ConnectionBluetooth {
        get {
            return Get(nameof(ConnectionBluetooth));
        }
    }

    public static string BatteryCharging {
        get {
            return Get(nameof(BatteryCharging));
        }
    }

    public static string FnLockEnabled {
        get {
            return Get(nameof(FnLockEnabled));
        }
    }

    public static string FnLockDisabled {
        get {
            return Get(nameof(FnLockDisabled));
        }
    }

    public static string StatusBatteryLabel {
        get {
            return Get(nameof(StatusBatteryLabel));
        }
    }

    public static string StatusConnectionLabel {
        get {
            return Get(nameof(StatusConnectionLabel));
        }
    }

    public static string StatusFnLockLabel {
        get {
            return Get(nameof(StatusFnLockLabel));
        }
    }


    public static string KeysModelLabel {
        get {
            return Get(nameof(KeysModelLabel));
        }
    }

    public static string KeysModel_MagicKeyboard {
        get {
            return Get(nameof(KeysModel_MagicKeyboard));
        }
    }

    public static string KeysModel_MagicKeyboardWithNumpad {
        get {
            return Get(nameof(KeysModel_MagicKeyboardWithNumpad));
        }
    }

    public static string KeyActionSetMapping {
        get {
            return Get(nameof(KeyActionSetMapping));
        }
    }

    public static string KeyActionRemoveMapping {
        get {
            return Get(nameof(KeyActionRemoveMapping));
        }
    }

    public static string KeyActionCancel {
        get {
            return Get(nameof(KeyActionCancel));
        }
    }

    public static string KeyOptionWithFn {
        get {
            return Get(nameof(KeyOptionWithFn));
        }
    }

    public static string KeyOptionFollowFnLock {
        get {
            return Get(nameof(KeyOptionFollowFnLock));
        }
    }

    public static string KeyOptionFollowFnLockHint {
        get {
            return Get(nameof(KeyOptionFollowFnLockHint));
        }
    }

    public static string KeyNotRemapped {
        get {
            return Get(nameof(KeyNotRemapped));
        }
    }

    public static string KeyCannotRemap {
        get {
            return Get(nameof(KeyCannotRemap));
        }
    }

    public static string KeyPickPrompt {
        get {
            return Get(nameof(KeyPickPrompt));
        }
    }

    public static string KeyRuleWithFn {
        get {
            return Get(nameof(KeyRuleWithFn));
        }
    }

    public static string KeyRuleFollowsFnLock {
        get {
            return Get(nameof(KeyRuleFollowsFnLock));
        }
    }

    public static string ApplyApplied {
        get {
            return Get(nameof(ApplyApplied));
        }
    }

    public static string ApplyIgnored {
        get {
            return Get(nameof(ApplyIgnored));
        }
    }

    public static string ApplySavedNotApplied {
        get {
            return Get(nameof(ApplySavedNotApplied));
        }
    }

    public static string ApplyDriverError {
        get {
            return Get(nameof(ApplyDriverError));
        }
    }

    public static string ApplySaveFailed {
        get {
            return Get(nameof(ApplySaveFailed));
        }
    }

    public static string HotkeyAgentNotRunning {
        get {
            return Get(nameof(HotkeyAgentNotRunning));
        }
    }

    public static string HotkeyNotRegistered {
        get {
            return Get(nameof(HotkeyNotRegistered));
        }
    }

    public static string HotkeyAlreadyAssigned {
        get {
            return Get(nameof(HotkeyAlreadyAssigned));
        }
    }

    public static string HotkeyDeleted {
        get {
            return Get(nameof(HotkeyDeleted));
        }
    }

    public static string HotkeyUndo {
        get {
            return Get(nameof(HotkeyUndo));
        }
    }

    public static string HotkeyAdd {
        get {
            return Get(nameof(HotkeyAdd));
        }
    }

    public static string HotkeyAddFirst {
        get {
            return Get(nameof(HotkeyAddFirst));
        }
    }

    public static string HotkeyCapturePrompt {
        get {
            return Get(nameof(HotkeyCapturePrompt));
        }
    }

    public static string HotkeyNotSet {
        get {
            return Get(nameof(HotkeyNotSet));
        }
    }

    public static string HotkeyBrowse {
        get {
            return Get(nameof(HotkeyBrowse));
        }
    }

    public static string HotkeyBrowseFilter {
        get {
            return Get(nameof(HotkeyBrowseFilter));
        }
    }

    public static string HotkeyStartAgent {
        get {
            return Get(nameof(HotkeyStartAgent));
        }
    }

    public static string HotkeyAgentNotFound {
        get {
            return Get(nameof(HotkeyAgentNotFound));
        }
    }

    public static string HotkeyEdit {
        get {
            return Get(nameof(HotkeyEdit));
        }
    }

    public static string HotkeyDone {
        get {
            return Get(nameof(HotkeyDone));
        }
    }

    public static string HotkeyDelete {
        get {
            return Get(nameof(HotkeyDelete));
        }
    }

    public static string FnLockToggleLabel {
        get {
            return Get(nameof(FnLockToggleLabel));
        }
    }

    public static string FnLockToggleHint {
        get {
            return Get(nameof(FnLockToggleHint));
        }
    }

    public static string OptionsAppearance {
        get {
            return Get(nameof(OptionsAppearance));
        }
    }

    public static string OptionsTheme {
        get {
            return Get(nameof(OptionsTheme));
        }
    }

    public static string OptionsThemeSystem {
        get {
            return Get(nameof(OptionsThemeSystem));
        }
    }

    public static string OptionsThemeDark {
        get {
            return Get(nameof(OptionsThemeDark));
        }
    }

    public static string OptionsThemeLight {
        get {
            return Get(nameof(OptionsThemeLight));
        }
    }

    public static string OptionsLanguage {
        get {
            return Get(nameof(OptionsLanguage));
        }
    }

    public static string OptionsLanguageSystem {
        get {
            return Get(nameof(OptionsLanguageSystem));
        }
    }

    public static string OptionsLanguageRestart {
        get {
            return Get(nameof(OptionsLanguageRestart));
        }
    }

    public static string OptionsRestart {
        get {
            return Get(nameof(OptionsRestart));
        }
    }

    public static string OptionsRestartFailed {
        get {
            return Get(nameof(OptionsRestartFailed));
        }
    }

    public static string OptionsSaveFailed {
        get {
            return Get(nameof(OptionsSaveFailed));
        }
    }

    public static string OptionsAgent {
        get {
            return Get(nameof(OptionsAgent));
        }
    }

    public static string OptionsAgentStatus {
        get {
            return Get(nameof(OptionsAgentStatus));
        }
    }

    public static string OptionsAgentHint {
        get {
            return Get(nameof(OptionsAgentHint));
        }
    }

    public static string OptionsAgentRunning {
        get {
            return Get(nameof(OptionsAgentRunning));
        }
    }

    public static string OptionsAgentStopped {
        get {
            return Get(nameof(OptionsAgentStopped));
        }
    }

    public static string OptionsAgentStarting {
        get {
            return Get(nameof(OptionsAgentStarting));
        }
    }

    public static string OptionsAgentNoAnswer {
        get {
            return Get(nameof(OptionsAgentNoAnswer));
        }
    }

    public static string OptionsAutostart {
        get {
            return Get(nameof(OptionsAutostart));
        }
    }

    public static string OptionsAutostartHint {
        get {
            return Get(nameof(OptionsAutostartHint));
        }
    }

    public static string OptionsAutostartFailed {
        get {
            return Get(nameof(OptionsAutostartFailed));
        }
    }

    public static string OptionsSystem {
        get {
            return Get(nameof(OptionsSystem));
        }
    }

    public static string OptionsDriver {
        get {
            return Get(nameof(OptionsDriver));
        }
    }

    public static string OptionsDriverActive {
        get {
            return Get(nameof(OptionsDriverActive));
        }
    }

    public static string OptionsDriverIdle {
        get {
            return Get(nameof(OptionsDriverIdle));
        }
    }

    public static string OptionsDriverMissing {
        get {
            return Get(nameof(OptionsDriverMissing));
        }
    }

    public static string OptionsKeyboard {
        get {
            return Get(nameof(OptionsKeyboard));
        }
    }

    public static string OptionsAppVersion {
        get {
            return Get(nameof(OptionsAppVersion));
        }
    }

    public static string OptionsDriverVersion {
        get {
            return Get(nameof(OptionsDriverVersion));
        }
    }

    public static string KeyboardNotConnected {
        get {
            return Get(nameof(KeyboardNotConnected));
        }
    }

    public static string LinkPromptOne {
        get {
            return Get(nameof(LinkPromptOne));
        }
    }

    public static string LinkPromptSeveral {
        get {
            return Get(nameof(LinkPromptSeveral));
        }
    }

    public static string LinkPromptYes {
        get {
            return Get(nameof(LinkPromptYes));
        }
    }

    public static string LinkPromptNo {
        get {
            return Get(nameof(LinkPromptNo));
        }
    }

    public static string KeysNoKeyboard {
        get {
            return Get(nameof(KeysNoKeyboard));
        }
    }

    public static string OptionsOpenForNewKeyboard {
        get {
            return Get(nameof(OptionsOpenForNewKeyboard));
        }
    }

    public static string OptionsOpenForNewKeyboardHint {
        get {
            return Get(nameof(OptionsOpenForNewKeyboardHint));
        }
    }

    private static string Get(string key) {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
