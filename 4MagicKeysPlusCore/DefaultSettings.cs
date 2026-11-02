namespace _4MagicKeysPlus.Core;

// Source and Target are the raw values of the driver (see Key Rules in the README); Comment is only for the reader of the file.
public sealed record DefaultRule(uint Source, uint Target, bool FnLockAware, string? Comment);

public sealed record DefaultSettings(KeyboardProduct Model, bool FnLock, IReadOnlyList<DefaultRule> Rules);
