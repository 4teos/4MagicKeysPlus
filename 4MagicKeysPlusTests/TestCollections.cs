namespace _4MagicKeysPlusTests;

internal static class TestCollections {
    // The registry tests share the scratch key under HKCU\Software\4MagicKeysPlusTests, and each
    // one deletes it when it is left empty, so they must not run side by side.
    public const string Registry = "Registry";
}
