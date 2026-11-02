using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlusTests;

public class KeyboardModelTests {
    private static readonly LayoutStandard[] Standards = Enum.GetValues<LayoutStandard>();

    [Fact]
    public void EveryProduct_HasAModelWithADescription() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            Assert.False(string.IsNullOrWhiteSpace(KeyboardModel.From(product).DeviceDescription), product.ToString());
        }
    }

    [Fact]
    public void EveryProduct_HasAModelOfItsOwn() {
        KeyboardProduct[] products = Enum.GetValues<KeyboardProduct>();

        Assert.Equal(products.Length, products.Select(KeyboardModel.From).Distinct().Count());
        Assert.Equal(products.Length, products.Select(product => KeyboardModel.From(product).DeviceDescription).Distinct().Count());
    }

    [Fact]
    public void EveryProduct_HasALayoutOfEachStandard() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            foreach (LayoutStandard standard in Standards) {
                Assert.NotEmpty(KeyboardLayout.GetKeys(KeyboardModel.From(product), standard));
            }
        }
    }

    private static IReadOnlyList<RowLayoutEntry> Rows(KeyboardProduct product, LayoutStandard standard) {
        return KeyboardLayout.GetKeys(KeyboardModel.From(product), standard);
    }

    private static IReadOnlyList<KeyLayoutEntry> Keys(KeyboardProduct product, LayoutStandard standard) {
        return [.. Rows(product, standard).SelectMany(row => row.RowKeys)];
    }

    private static IReadOnlyList<KeyLayoutEntry> KeysWithout(KeyboardProduct product, LayoutStandard standard, PhysicalKey key) {
        return [.. Keys(product, standard).Where(entry => entry.Kind != key)];
    }

    private static double Width(RowLayoutEntry row) {
        return row.RowKeys.Sum(key => key.Width.Value + key.GapAfter.Value);
    }

    [Theory]
    [InlineData(KeyboardProduct.A1644, PhysicalKey.Eject)]
    [InlineData(KeyboardProduct.A1843, PhysicalKey.Eject)]
    [InlineData(KeyboardProduct.A2449, PhysicalKey.TouchId)]
    [InlineData(KeyboardProduct.A2450, PhysicalKey.Lock)]
    [InlineData(KeyboardProduct.A2520, PhysicalKey.TouchId)]
    [InlineData(KeyboardProduct.A3118, PhysicalKey.TouchId)]
    [InlineData(KeyboardProduct.A3119, PhysicalKey.TouchId)]
    [InlineData(KeyboardProduct.A3203, PhysicalKey.Lock)]
    public void TheTopRightKey_IsTheOneOfTheModel(KeyboardProduct product, PhysicalKey expected) {
        PhysicalKey[] topRightKeys = [PhysicalKey.Eject, PhysicalKey.Lock, PhysicalKey.TouchId];

        foreach (LayoutStandard standard in Standards) {
            Assert.Equal([expected], Keys(product, standard).Select(entry => entry.Kind).Where(topRightKeys.Contains));
        }
    }

    [Fact]
    public void EveryModel_DrawsEachKeyOnce_ExceptTheTwoPartKeys() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            foreach (LayoutStandard standard in Standards) {
                foreach (IGrouping<PhysicalKey, KeyLayoutEntry> group in Keys(product, standard).GroupBy(entry => entry.Kind)) {
                    int expected = group.Key == PhysicalKey.NumPadEnter || (group.Key == PhysicalKey.Enter && standard == LayoutStandard.Iso) ? 2 : 1;

                    Assert.True(group.Count() == expected, $"{product}, {standard}: {group.Key} is drawn {group.Count()} times");
                }
            }
        }
    }

    [Fact]
    public void TheTopRightKey_IsTheOnlyDifferenceOfModelsThatShareALayout() {
        foreach (LayoutStandard standard in Standards) {
            Assert.Equal(KeysWithout(KeyboardProduct.A1644, standard, PhysicalKey.Eject), KeysWithout(KeyboardProduct.A3118, standard, PhysicalKey.TouchId));
            Assert.Equal(KeysWithout(KeyboardProduct.A1644, standard, PhysicalKey.Eject), KeysWithout(KeyboardProduct.A3203, standard, PhysicalKey.Lock));
            Assert.Equal(KeysWithout(KeyboardProduct.A1843, standard, PhysicalKey.Eject), KeysWithout(KeyboardProduct.A2520, standard, PhysicalKey.TouchId));
            Assert.Equal(Keys(KeyboardProduct.A3118, standard), Keys(KeyboardProduct.A2449, standard));
            Assert.Equal(Keys(KeyboardProduct.A3203, standard), Keys(KeyboardProduct.A2450, standard));
        }
    }

    [Fact]
    public void OnlyTheModelsWithANumpad_HaveItsKeys() {
        KeyboardProduct[] withNumpad = [KeyboardProduct.A1843, KeyboardProduct.A2520, KeyboardProduct.A3119];

        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            foreach (LayoutStandard standard in Standards) {
                Assert.True(withNumpad.Contains(product) == Keys(product, standard).Any(entry => entry.Kind == PhysicalKey.NumPad5), $"{product}, {standard}");
            }
        }
    }

    [Fact]
    public void TheMenuKey_TakesTheNavigationBlockPlaceOfFnAndFnMovesToTheBottomLeft() {
        foreach (LayoutStandard standard in Standards) {
            IReadOnlyList<RowLayoutEntry> withMenu = Rows(KeyboardProduct.A3119, standard);
            IReadOnlyList<RowLayoutEntry> withoutMenu = Rows(KeyboardProduct.A2520, standard);

            Assert.Equal(PhysicalKey.Menu, withMenu[1].RowKeys[14].Kind);
            Assert.Equal(PhysicalKey.Fn, withoutMenu[1].RowKeys[14].Kind);
            Assert.Equal(PhysicalKey.Fn, withMenu[5].RowKeys[0].Kind);
            Assert.Equal(PhysicalKey.LeftControl, withoutMenu[5].RowKeys[0].Kind);
            Assert.DoesNotContain(Keys(KeyboardProduct.A1843, standard), entry => entry.Kind == PhysicalKey.Menu);
        }
    }

    [Fact]
    public void TheAnsiLayout_HasEveryKeyOfTheIsoOneButSection() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            HashSet<PhysicalKey> ansi = [.. Keys(product, LayoutStandard.Ansi).Select(entry => entry.Kind)];
            HashSet<PhysicalKey> iso = [.. Keys(product, LayoutStandard.Iso).Select(entry => entry.Kind)];

            Assert.Equal([PhysicalKey.Section], iso.Except(ansi));
            Assert.Empty(ansi.Except(iso));
        }
    }

    [Fact]
    public void EveryRow_IsAsWideInAnsiAsInIso() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            IReadOnlyList<RowLayoutEntry> ansi = Rows(product, LayoutStandard.Ansi);
            IReadOnlyList<RowLayoutEntry> iso = Rows(product, LayoutStandard.Iso);

            for (int row = 0; row < iso.Count; row++) {
                Assert.True(Width(ansi[row]) == Width(iso[row]), $"{product}, row {row + 1}: {Width(ansi[row])} against {Width(iso[row])}");
            }
        }
    }

    [Fact]
    public void TheTopLeftKey_IsTildeInAnsiAndSectionInIso() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            Assert.Equal(PhysicalKey.Tilde, Rows(product, LayoutStandard.Ansi)[1].RowKeys[0].Kind);
            Assert.Equal(PhysicalKey.Section, Rows(product, LayoutStandard.Iso)[1].RowKeys[0].Kind);
        }
    }

    [Fact]
    public void TheAnsiEnter_IsOneKeyOfTheRowOfCapsLockAsWideAsCapsLock() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            IReadOnlyList<RowLayoutEntry> rows = Rows(product, LayoutStandard.Ansi);
            KeyLayoutEntry enter = rows[3].RowKeys.Single(entry => entry.Kind == PhysicalKey.Enter);

            Assert.Equal(KeyRowPosition.NORMAL, enter.KeyRowPosition);
            Assert.Equal(rows[3].RowKeys.Single(entry => entry.Kind == PhysicalKey.CapsLock).Width.Value, enter.Width.Value);
            Assert.DoesNotContain(rows[2].RowKeys, entry => entry.Kind == PhysicalKey.Enter);
        }
    }

    [Fact]
    public void TheBackslash_EndsTheRowOfTabInAnsiAndTheRowOfCapsLockInIso() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            Assert.Equal(PhysicalKey.Backslash, Rows(product, LayoutStandard.Ansi)[2].RowKeys[13].Kind);
            Assert.Contains(Rows(product, LayoutStandard.Iso)[3].RowKeys, entry => entry.Kind == PhysicalKey.Backslash);
        }
    }

    [Fact]
    public void TheAnsiShifts_AreAsWideAsEachOtherAndNothingIsLeftOfZ() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            IReadOnlyList<KeyLayoutEntry> row = Rows(product, LayoutStandard.Ansi)[4].RowKeys;

            Assert.Equal(row.Single(entry => entry.Kind == PhysicalKey.RightShift).Width.Value, row[0].Width.Value);
            Assert.Equal(PhysicalKey.LeftShift, row[0].Kind);
            Assert.Equal(PhysicalKey.Z, row[1].Kind);
        }
    }
}
