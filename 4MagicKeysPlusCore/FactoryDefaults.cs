namespace _4MagicKeysPlus.Core;

// A key rule as the driver reads it from the registry: the raw Source and Target values (see Key Rules in the README). The
// comment is only for the reader of the file.
public sealed record FactoryRule(uint Source, uint Target, bool FnLockAware, string? Comment);

// What a keyboard of a model starts with: its Fn Lock and its rules. The driver gives them to a keyboard that has no profile,
// and a new profile begins as a copy of them.
public sealed record FactoryDefaults(KeyboardProduct Model, bool FnLock, IReadOnlyList<FactoryRule> Rules);
