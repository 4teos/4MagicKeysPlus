using System.Windows;
using System.Windows.Controls.Primitives;

namespace _4MagicKeysPlus.Views;

public static class TooltipPlacement {
    public static readonly CustomPopupPlacementCallback CenteredBelow = (popupSize, targetSize, _) =>
        new[] {
            new CustomPopupPlacement(
                new Point((targetSize.Width - popupSize.Width) / 2, targetSize.Height + 6),
                PopupPrimaryAxis.None),
        };

    public static readonly CustomPopupPlacementCallback CenteredAbove = (popupSize, targetSize, _) =>
        new[] {
            new CustomPopupPlacement(
                new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 6),
                PopupPrimaryAxis.None),
        };
}
