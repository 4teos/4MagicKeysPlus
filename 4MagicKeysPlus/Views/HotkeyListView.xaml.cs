using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlus.Views;

public partial class HotkeyListView : UserControl {
    private static readonly TimeSpan UndoDuration = TimeSpan.FromSeconds(6);
    // The agent gives up a pause after 30 seconds without a word, so capturing stops before that.
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(25);
    private static readonly Duration SlideDuration = new(TimeSpan.FromMilliseconds(140));
    private const double AutoScrollZone = 48;
    private const double AutoScrollMaxStep = 16;

    private readonly HotkeyEditor editor;
    private readonly HotkeyListViewModel list;
    private readonly DispatcherTimer undoTimer = new() { Interval = UndoDuration };
    private readonly DispatcherTimer captureTimer = new() { Interval = CaptureTimeout };
    private readonly DispatcherTimer autoScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(20) };
    private HotkeyCardViewModel? capturingCard;
    private IAgentPause? pause;
    private int captureGeneration;
    private Point dragStart;
    private HotkeyCardViewModel? dragCandidate;
    private DragState? drag;
    private Point lastPointer;
    private Window? hostWindow;

    public HotkeyListView(HotkeyEditor editor) {
        InitializeComponent();
        this.editor = editor;
        list = new HotkeyListViewModel(editor, AppIcons.Load, AgentLauncher.TryStart);
        DataContext = list;

        undoTimer.Tick += (_, _) => {
            undoTimer.Stop();
            list.ClearUndo();
        };
        captureTimer.Tick += async (_, _) => await EndCaptureAsync();
        autoScrollTimer.Tick += (_, _) => AutoScroll();

        Loaded += async (_, _) => {
            hostWindow = Window.GetWindow(this);
            hostWindow?.PreviewMouseDown += HostWindow_PreviewMouseDown;
            await list.RefreshAsync();
        };
        Unloaded += async (_, _) => {
            hostWindow?.PreviewMouseDown -= HostWindow_PreviewMouseDown;
            hostWindow = null;
            await EndCaptureAsync();
            foreach (HotkeyCardViewModel card in list.Cards.Where(card => card.IsEditing).ToList()) {
                await CommitOrDiscardAsync(card);
            }
        };
    }

    // A click outside the cards being edited applies them.
    private async void HostWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
        HotkeyCardViewModel? clicked = CardOf(e.OriginalSource as DependencyObject);
        foreach (HotkeyCardViewModel card in list.Cards.Where(card => card.IsEditing && card != clicked).ToList()) {
            await CommitOrDiscardAsync(card);
        }
    }

    // A blank card the user did not touch just goes away; anything else is saved or, when it
    // cannot be, stays open and says why.
    private async Task CommitOrDiscardAsync(HotkeyCardViewModel card) {
        if (capturingCard == card) {
            await EndCaptureAsync();
        }
        if (card.IsUntouched) {
            list.Cancel(card);
        } else {
            await list.CommitAsync(card);
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e) {
        base.OnPreviewKeyDown(e);

        if (drag is not null) {
            if (e.Key == Key.Escape) {
                _ = EndDragAsync(commit: false);
            }
            e.Handled = true;
            return;
        }

        if (capturingCard is { } capturing) {
            Key key = e.Key switch {
                Key.System => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                Key.DeadCharProcessed => e.DeadCharProcessedKey,
                _ => e.Key,
            };
            e.Handled = true;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) {
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.None && key == Key.Escape) {
                _ = EndCaptureAsync();
            } else if (Keyboard.Modifiers == ModifierKeys.None && key == Key.Back) {
                Capture(capturing, Key.None, ModifierKeys.None);
            } else if (key != Key.Snapshot) {
                Capture(capturing, key, Keyboard.Modifiers);
            }
            return;
        }

        HotkeyCardViewModel? editing = CardOf(Keyboard.FocusedElement as DependencyObject);
        if (editing is { IsEditing: true }) {
            if (e.Key == Key.Escape) {
                list.Cancel(editing);
                e.Handled = true;
            } else if (e.Key == Key.Enter && e.OriginalSource is TextBox) {
                _ = list.CommitAsync(editing);
                e.Handled = true;
            }
        }
    }

    // Print Screen only ever sends the release of the key.
    protected override void OnPreviewKeyUp(KeyEventArgs e) {
        base.OnPreviewKeyUp(e);
        if (capturingCard is { } capturing && e.Key == Key.Snapshot) {
            Capture(capturing, Key.Snapshot, Keyboard.Modifiers);
            e.Handled = true;
        }
    }

    // ModifierKeys has the same bit values as the MOD_* flags the agent registers with.
    private void Capture(HotkeyCardViewModel card, Key key, ModifierKeys modifiers) {
        int virtualKey = key == Key.None ? 0 : KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0 && key != Key.None) {
            return;
        }
        card.SetDraftCombination((uint)virtualKey, virtualKey == 0 ? 0 : (uint)modifiers);
        list.UpdateConflict(card);
        _ = EndCaptureAsync();
    }

    private async Task StartCaptureAsync(HotkeyCardViewModel card) {
        if (capturingCard == card) {
            return;
        }
        await EndCaptureAsync();

        int generation = ++captureGeneration;
        CardsHost.UpdateLayout();
        Border? chip = ChipOf(card);
        chip?.BringIntoView();
        Keyboard.Focus(chip);

        // Keys are taken only once the agent has let go of its hotkeys.
        IAgentPause? agentPause = await editor.PauseAgentAsync();
        if (generation != captureGeneration || !card.IsEditing) {
            agentPause?.Dispose();
            return;
        }

        pause = agentPause;
        capturingCard = card;
        card.SetCapturing(true);
        captureTimer.Start();
    }

    private async Task EndCaptureAsync() {
        captureGeneration++;
        captureTimer.Stop();
        HotkeyCardViewModel? card = capturingCard;
        IAgentPause? current = pause;
        capturingCard = null;
        pause = null;
        card?.SetCapturing(false);

        if (current is not null) {
            await current.ResumeAsync();
            current.Dispose();
        }
    }

    // A card is dragged by its grip: a translucent copy of it follows the pointer, its place in the
    // list stays as an empty slot, and the other cards move aside as the copy passes them.
    private void Grip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        dragStart = e.GetPosition(null);
        dragCandidate = CardFrom(sender);
    }

    private void Grip_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        dragCandidate = null;
    }

    private void Grip_PreviewMouseMove(object sender, MouseEventArgs e) {
        if (dragCandidate is not { } card || e.LeftButton != MouseButtonState.Pressed) {
            dragCandidate = null;
            return;
        }

        Point position = e.GetPosition(null);
        if (Math.Abs(position.X - dragStart.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(position.Y - dragStart.Y) >= SystemParameters.MinimumVerticalDragDistance) {
            dragCandidate = null;
            BeginDrag(card, e.GetPosition(Scroller));
        }
    }

    private void BeginDrag(HotkeyCardViewModel card, Point pointer) {
        if (ChildNamed<Border>(ContainerOf(card), "Card") is not { } cardBorder) {
            return;
        }

        double width = cardBorder.ActualWidth;
        double height = cardBorder.ActualHeight;
        Point topLeft = cardBorder.TransformToVisual(Scroller).Transform(new Point(0, 0));

        // The copy is a picture taken now, before the card itself turns into the empty slot.
        double scale = VisualTreeHelper.GetDpi(cardBorder).DpiScaleX;
        DrawingVisual drawing = new();
        using (DrawingContext context = drawing.RenderOpen()) {
            context.DrawRectangle(new VisualBrush(cardBorder), null, new Rect(0, 0, width, height));
        }
        RenderTargetBitmap picture = new((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        picture.Render(drawing);

        Ghost.Width = width;
        Ghost.Height = height;
        Ghost.Background = new ImageBrush(picture);
        Canvas.SetLeft(Ghost, topLeft.X);
        Canvas.SetTop(Ghost, topLeft.Y);
        DragLayer.Visibility = Visibility.Visible;

        drag = new DragState(card, list.Cards.IndexOf(card), pointer, topLeft.Y, height);
        lastPointer = pointer;
        card.SetDragging(true);
        Mouse.OverrideCursor = GrabCursors.Closed;
        CaptureMouse();
        autoScrollTimer.Start();
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (drag is not null) {
            lastPointer = e.GetPosition(Scroller);
            UpdateDrag();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonUp(e);
        if (drag is not null) {
            _ = EndDragAsync(commit: true);
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e) {
        base.OnLostMouseCapture(e);
        if (drag is not null) {
            _ = EndDragAsync(commit: false);
        }
    }

    // Moves the copy with the pointer, and the card's slot to where the copy has got to.
    private void UpdateDrag() {
        if (drag is not { } state) {
            return;
        }

        double top = Math.Clamp(state.GhostTop + lastPointer.Y - state.Pointer.Y, 0, Math.Max(0, Scroller.ActualHeight - state.GhostHeight));
        Canvas.SetTop(Ghost, top);

        double center = Scroller.TransformToVisual(CardsHost).Transform(new Point(0, top + state.GhostHeight / 2)).Y;
        int target = list.Cards.Where(other => other != state.Card).Count(other => SlotCenter(other) < center);
        if (target != list.Cards.IndexOf(state.Card)) {
            AnimateReorder(() => list.MoveTo(state.Card, target));
        }
    }

    // Near the top or bottom edge of the list, dragging scrolls it.
    private void AutoScroll() {
        if (drag is null) {
            return;
        }

        double speed = 0;
        if (lastPointer.Y < AutoScrollZone) {
            speed = -(AutoScrollZone - lastPointer.Y) / AutoScrollZone * AutoScrollMaxStep;
        } else if (lastPointer.Y > Scroller.ActualHeight - AutoScrollZone) {
            speed = (lastPointer.Y - (Scroller.ActualHeight - AutoScrollZone)) / AutoScrollZone * AutoScrollMaxStep;
        }
        if (speed != 0) {
            Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset + speed);
            UpdateDrag();
        }
    }

    private async Task EndDragAsync(bool commit) {
        if (drag is not { } state) {
            return;
        }
        drag = null;
        autoScrollTimer.Stop();
        Mouse.OverrideCursor = null;
        ReleaseMouseCapture();
        DragLayer.Visibility = Visibility.Collapsed;
        state.Card.SetDragging(false);

        if (commit) {
            await list.SaveOrderAsync(state.Card, state.StartIndex);
        } else {
            AnimateReorder(() => list.MoveTo(state.Card, state.StartIndex));
        }
    }

    // Changes the order of the cards and lets each one slide from where it was to where it is now.
    private void AnimateReorder(Action reorder) {
        Dictionary<HotkeyCardViewModel, double> before = list.Cards.ToDictionary(card => card, card => SlotTop(card) + OffsetOf(card));
        reorder();
        CardsHost.UpdateLayout();

        foreach (HotkeyCardViewModel card in list.Cards) {
            if (ContainerOf(card) is not { } container || !before.TryGetValue(card, out double was)) {
                continue;
            }
            double delta = was - SlotTop(card);
            if (Math.Abs(delta) < 0.5) {
                continue;
            }

            TranslateTransform slide = new(0, delta);
            container.RenderTransform = slide;
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(delta, 0, SlideDuration) {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
        }
    }

    private FrameworkElement? ContainerOf(HotkeyCardViewModel card) {
        return CardsHost.ItemContainerGenerator.ContainerFromItem(card) as FrameworkElement;
    }

    // Where a card's slot is in the list, whatever it is sliding through at the moment.
    private double SlotTop(HotkeyCardViewModel card) {
        return ContainerOf(card) is { } container ? LayoutInformation.GetLayoutSlot(container).Top : 0;
    }

    private double SlotCenter(HotkeyCardViewModel card) {
        if (ContainerOf(card) is not { } container) {
            return 0;
        }
        Rect slot = LayoutInformation.GetLayoutSlot(container);
        return slot.Top + slot.Height / 2;
    }

    private double OffsetOf(HotkeyCardViewModel card) {
        return (ContainerOf(card)?.RenderTransform as TranslateTransform)?.Y ?? 0;
    }

    private sealed record DragState(HotkeyCardViewModel Card, int StartIndex, Point Pointer, double GhostTop, double GhostHeight);

    private async void Add_Click(object sender, RoutedEventArgs e) {
        HotkeyCardViewModel card = list.AddCard();
        await StartCaptureAsync(card);
    }

    private async void Action_Click(object sender, RoutedEventArgs e) {
        HotkeyCardViewModel card = CardFrom(sender);
        if (card.IsEditing) {
            await CommitOrDiscardAsync(card);
        } else {
            card.BeginEdit();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e) {
        HotkeyCardViewModel card = CardFrom(sender);
        if (capturingCard == card) {
            await EndCaptureAsync();
        }
        await list.DeleteAsync(card);
        if (list.CanUndo) {
            undoTimer.Stop();
            undoTimer.Start();
        }
    }

    private async void Undo_Click(object sender, RoutedEventArgs e) {
        undoTimer.Stop();
        await list.UndoDeleteAsync();
    }

    private async void StartAgent_Click(object sender, RoutedEventArgs e) {
        await list.StartAgentAsync();
    }

    private void Browse_Click(object sender, RoutedEventArgs e) {
        HotkeyCardViewModel card = CardFrom(sender);
        Microsoft.Win32.OpenFileDialog dialog = new() { Filter = Strings.HotkeyBrowseFilter, CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) {
            card.SetDraftFile(dialog.FileName, HotkeyNames.FromPath(dialog.FileName));
        }
    }

    private async void Chip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        HotkeyCardViewModel card = CardFrom(sender);
        if (card.IsEditing) {
            e.Handled = true;
            await StartCaptureAsync(card);
        }
    }

    private async void Chip_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
        if (capturingCard is not null && capturingCard == CardFrom(sender)) {
            await EndCaptureAsync();
        }
    }

    private static HotkeyCardViewModel CardFrom(object sender) {
        return (HotkeyCardViewModel)((FrameworkElement)sender).DataContext;
    }

    // The card whose part of the visual tree the element is in, if any.
    private static HotkeyCardViewModel? CardOf(DependencyObject? element) {
        while (element is not null) {
            if (element is FrameworkElement { DataContext: HotkeyCardViewModel card }) {
                return card;
            }
            element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return null;
    }

    private Border? ChipOf(HotkeyCardViewModel card) {
        return ChildNamed<Border>(CardsHost.ItemContainerGenerator.ContainerFromItem(card), "Chip");
    }

    private static T? ChildNamed<T>(DependencyObject? parent, string name) where T : FrameworkElement {
        if (parent is null) {
            return null;
        }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found && found.Name == name) {
                return found;
            }
            T? nested = ChildNamed<T>(child, name);
            if (nested is not null) {
                return nested;
            }
        }
        return null;
    }
}
