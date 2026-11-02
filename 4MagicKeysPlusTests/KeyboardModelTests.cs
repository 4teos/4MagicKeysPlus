using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlusTests;

public class KeyboardModelTests {
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
    public void EveryProduct_HasALayout() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            Assert.NotEmpty(KeyboardLayout.GetKeys(KeyboardModel.From(product)));
        }
    }

    private static IReadOnlyList<KeyLayoutEntry> Keys(KeyboardProduct product) {
        return [.. KeyboardLayout.GetKeys(KeyboardModel.From(product)).SelectMany(row => row.RowKeys)];
    }

    private static IReadOnlyList<KeyLayoutEntry> KeysWithout(KeyboardProduct product, PhysicalKey key) {
        return [.. Keys(product).Where(entry => entry.Kind != key)];
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

        Assert.Equal([expected], Keys(product).Select(entry => entry.Kind).Where(topRightKeys.Contains));
    }

    [Fact]
    public void EveryModel_DrawsEachKeyOnce_ExceptTheTwoPartKeys() {
        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            foreach (IGrouping<PhysicalKey, KeyLayoutEntry> group in Keys(product).GroupBy(entry => entry.Kind)) {
                int expected = group.Key is PhysicalKey.Enter or PhysicalKey.NumPadEnter ? 2 : 1;

                Assert.True(group.Count() == expected, $"{product}: {group.Key} is drawn {group.Count()} times");
            }
        }
    }

    [Fact]
    public void TheTopRightKey_IsTheOnlyDifferenceOfModelsThatShareALayout() {
        Assert.Equal(KeysWithout(KeyboardProduct.A1644, PhysicalKey.Eject), KeysWithout(KeyboardProduct.A3118, PhysicalKey.TouchId));
        Assert.Equal(KeysWithout(KeyboardProduct.A1644, PhysicalKey.Eject), KeysWithout(KeyboardProduct.A3203, PhysicalKey.Lock));
        Assert.Equal(KeysWithout(KeyboardProduct.A1843, PhysicalKey.Eject), KeysWithout(KeyboardProduct.A2520, PhysicalKey.TouchId));
        Assert.Equal(Keys(KeyboardProduct.A3118), Keys(KeyboardProduct.A2449));
        Assert.Equal(Keys(KeyboardProduct.A3203), Keys(KeyboardProduct.A2450));
    }

    [Fact]
    public void OnlyTheModelsWithANumpad_HaveItsKeys() {
        KeyboardProduct[] withNumpad = [KeyboardProduct.A1843, KeyboardProduct.A2520, KeyboardProduct.A3119];

        foreach (KeyboardProduct product in Enum.GetValues<KeyboardProduct>()) {
            Assert.True(withNumpad.Contains(product) == Keys(product).Any(entry => entry.Kind == PhysicalKey.NumPad5), product.ToString());
        }
    }

    [Fact]
    public void TheMenuKey_TakesTheNavigationBlockPlaceOfFnAndFnMovesToTheBottomLeft() {
        IReadOnlyList<RowLayoutEntry> withMenu = KeyboardLayout.GetKeys(KeyboardModel.A3119);
        IReadOnlyList<RowLayoutEntry> withoutMenu = KeyboardLayout.GetKeys(KeyboardModel.A2520);

        Assert.Equal(PhysicalKey.Menu, withMenu[1].RowKeys[14].Kind);
        Assert.Equal(PhysicalKey.Fn, withoutMenu[1].RowKeys[14].Kind);
        Assert.Equal(PhysicalKey.Fn, withMenu[5].RowKeys[0].Kind);
        Assert.Equal(PhysicalKey.LeftControl, withoutMenu[5].RowKeys[0].Kind);
        Assert.DoesNotContain(Keys(KeyboardProduct.A1843), entry => entry.Kind == PhysicalKey.Menu);
    }
}
