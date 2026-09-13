using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Greenlight.TestStrip;

/// <summary>
/// The strip: a small undecorated window that can be dragged anywhere and clicked on.
/// </summary>
/// <remarks>
/// <para>
/// The opposite of every other client in the family — those are click-through by design, and
/// this one exists to be clicked. There is no title bar, so dragging is on the chassis: press
/// anywhere that is not a pad and the window moves. That means the whole strip is a handle
/// except for the six places where it is a button, which is why the layout is a tested object
/// rather than numbers inlined into the render.
/// </para>
/// <para>
/// The window stays open for as long as a hold is meant to last. Greenlight drops a hold when
/// the client that placed it detaches, so a strip that set red and exited would leave the light
/// telling the truth again a moment later — which is the one behaviour that would make this
/// tool useless for testing anything.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class StripWindow : Window
{
    private readonly StripConfig _config;
    private readonly StripCanvas _canvas;
    private readonly DispatcherTimer _frames;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private StripLayout _layout;
    private TimeSpan _lastFrame;
    private StripFrame _drawn;

    public StripWindow(StripConfig config, StripScene scene)
    {
        _config = config;
        Scene = scene;
        _layout = new StripLayout(config.Scale);

        Title = "Greenlight test strip";
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = config.Topmost;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SizeToContent = SizeToContent.Manual;

        // Transparent so the chassis can have rounded corners with nothing square behind them.
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Opacity = config.Opacity;

        _canvas = new StripCanvas(scene, config, _layout);
        Content = _canvas;

        Width = _layout.Width;
        Height = _layout.Height;

        // Slower than the desk toys on purpose. Nothing here follows a mouse; the fastest thing on
        // the strip is a breath about a second long, and 30fps of that is indistinguishable from
        // 60 while asking half as much of a machine that is busy running whatever is being tested.
        _frames = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _frames.Tick += OnFrame;
    }

    public StripScene Scene { get; }

    /// <summary>A pad was pressed. The app turns this into a hold or a release.</summary>
    public Action<Pad>? OnPadPressed { get; set; }

    /// <summary>The building toggle was pressed.</summary>
    public Action? OnToggleBuilding { get; set; }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        Place();

        _lastFrame = _clock.Elapsed;
        _frames.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _frames.Stop();
        RememberPosition();
        base.OnClosed(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(_canvas);

        var pad = _layout.HitTest(point.X, point.Y);
        if (pad is not null)
        {
            OnPadPressed?.Invoke(pad.Value);

            // A pad press is not a drag, however far the pointer wanders while the button is down.
            // Without this the strip creeps across the desk over an afternoon of prodding it.
            e.Handled = true;
            return;
        }

        if (_layout.HitsToggle(point.X, point.Y))
        {
            OnToggleBuilding?.Invoke();
            e.Handled = true;
            return;
        }

        // Anywhere else on the chassis is the handle. BeginMoveDrag hands the window to the window
        // manager for the rest of the gesture, which is what makes it snap and behave like any
        // other window rather than chasing the pointer a frame behind.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    /// <summary>Take up settings changed from the tray: size, opacity, topmost, colours.</summary>
    public void ApplyConfig()
    {
        _layout = new StripLayout(_config.Scale);

        // The canvas holds the layout it was built with, so a new scale means a new canvas. Cheaper
        // than making the layout mutable and having two objects that can disagree about where a pad is.
        Content = new StripCanvas(Scene, _config, _layout);

        Width = _layout.Width;
        Height = _layout.Height;
        Opacity = _config.Opacity;
        Topmost = _config.Topmost;

        InvalidateVisual();
    }

    /// <summary>Write where the strip is back to the config, so it comes back here next time.</summary>
    public void RememberPosition()
    {
        _config.X = Position.X;
        _config.Y = Position.Y;
        _config.Save();
    }

    /// <summary>Put the strip back where it was left, or centre it along the bottom on a first run.</summary>
    private void Place()
    {
        if (_config.X is { } x && _config.Y is { } y)
        {
            Position = new PixelPoint(x, y);

            // Only nudge it back on screen if it is entirely gone — a monitor that was unplugged
            // since last time. A strip hanging deliberately half off an edge is left where it is.
            if (IsOnAScreen(Position)) return;
        }

        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null) return;

        var area = screen.WorkingArea;
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;

        // Width is logical and the screen is physical, so the strip has to be scaled up before it
        // can be centred — otherwise it sits left of centre on every display that is not at 100%.
        var width = (int)Math.Round(Width * scaling);
        var height = (int)Math.Round(Height * scaling);

        Position = new PixelPoint(
            area.X + (area.Width - width) / 2,
            area.Y + area.Height - height - (int)Math.Round(64 * scaling));
    }

    private bool IsOnAScreen(PixelPoint position) =>
        Screens.All.Any(screen => screen.Bounds.Contains(position));

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = now - _lastFrame;
        _lastFrame = now;

        Scene.Advance(elapsed);

        // Only repaint when something would look different. A strip sitting on LIVE with nothing
        // attached is a still image, and repainting it thirty times a second is a fan spinning up
        // next to whatever it is you are actually trying to debug.
        var frame = Scene.Frame();
        if (frame == _drawn) return;

        _drawn = frame;
        (Content as StripCanvas)?.InvalidateVisual();
    }
}
