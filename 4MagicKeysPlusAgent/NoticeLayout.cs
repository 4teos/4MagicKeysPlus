namespace _4MagicKeysPlusAgent;

internal static class NoticeLayout {
    // Where a notice of the given size goes: the usual place (centered, at three quarters of the height of the work area) or,
    // when another notice is there, the first place above it (and above the ones stacked there) that is free. A place that a
    // notice that has closed left is used again.
    public static Point Place(Rectangle area, Size size, IReadOnlyList<Rectangle> open, int gap) {
        int x = area.Left + (area.Width - size.Width) / 2;
        int y = area.Top + area.Height * 3 / 4 - size.Height / 2;

        for (int attempt = 0; attempt <= open.Count; attempt++) {
            Rectangle wanted = Rectangle.Inflate(new Rectangle(x, y, size.Width, size.Height), 0, gap);
            List<Rectangle> blockers = [.. open.Where(wanted.IntersectsWith)];
            if (blockers.Count == 0) {
                break;
            }
            y = blockers.Min(blocker => blocker.Top) - gap - size.Height;
        }

        // Out of room above (many notices, a low screen): overlapping is better than leaving the screen.
        return new Point(x, Math.Max(y, area.Top));
    }
}
