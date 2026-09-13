namespace Greenlight.TestStrip;

/// <summary>A rectangle in the strip's own coordinates, in logical pixels from its top left.</summary>
public readonly record struct PadRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CentreX => X + Width / 2;
    public double CentreY => Y + Height / 2;

    /// <summary>Whether a point is inside. Used for hit-testing, so the edges count as in.</summary>
    public bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;
}

/// <summary>
/// Where everything sits on the strip: the grip, the five pads, the building toggle.
/// </summary>
/// <remarks>
/// Pure arithmetic and no Avalonia, for the same reason the scene is: the canvas draws these
/// rectangles and the window hit-tests the same ones, and the bug where those two disagree —
/// a pad that lights under the pointer but fires the one beside it — is invisible in a
/// screenshot and obvious in a test.
/// </remarks>
/// <param name="Scale">
/// Everything multiplied by this, so the whole instrument can be made bigger without a second
/// set of numbers to keep in step.
/// </param>
public sealed class StripLayout(double scale = 1.0)
{
    /// <summary>The pads, left to right. The order the strip is read in.</summary>
    public static readonly Pad[] Order = [Pad.Live, Pad.Off, Pad.Green, Pad.Amber, Pad.Red];

    private const double Padding = 10;      // chassis edge to anything inside it
    private const double Gap = 7;           // between pads
    private const double GripWidth = 14;    // the dotted handle on the left
    private const double PadWidth = 62;
    private const double PadHeight = 44;
    private const double ToggleWidth = 78;

    /// <summary>The bloom is drawn outside the pad, so the chassis needs room for it not to clip.</summary>
    /// <remarks>
    /// Kept under half the gap between pads. Reaching further looks better on a pad in isolation
    /// and turns the row into one smear the moment two pads are mid-changeover.
    /// </remarks>
    public const double GlowReach = 13;

    public double Scale { get; } = Math.Clamp(scale, 0.6, 3.0);

    /// <summary>The strip's overall size, which is what the window is set to.</summary>
    public double Width => (Padding * 2 + GripWidth + Gap
                            + PadWidth * Order.Length + Gap * Order.Length
                            + ToggleWidth) * Scale;

    public double Height => (Padding * 2 + PadHeight) * Scale;

    /// <summary>The dotted handle. Dragging anywhere that is not a pad moves the window, but this is what says so.</summary>
    public PadRect Grip => new(Padding * Scale, Padding * Scale, GripWidth * Scale, PadHeight * Scale);

    /// <summary>Where one pad sits.</summary>
    public PadRect RectFor(Pad pad)
    {
        var index = Array.IndexOf(Order, pad);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(pad), pad, "Not a pad on the strip.");

        var x = Padding + GripWidth + Gap + index * (PadWidth + Gap);
        return new PadRect(x * Scale, Padding * Scale, PadWidth * Scale, PadHeight * Scale);
    }

    /// <summary>The building toggle, on the right-hand end past the pads.</summary>
    public PadRect Toggle
    {
        get
        {
            var x = Padding + GripWidth + Gap + Order.Length * (PadWidth + Gap);
            return new PadRect(x * Scale, Padding * Scale, ToggleWidth * Scale, PadHeight * Scale);
        }
    }

    /// <summary>Which pad is under a point, or null for the chassis — which is where a drag starts.</summary>
    public Pad? HitTest(double x, double y)
    {
        foreach (var pad in Order)
            if (RectFor(pad).Contains(x, y))
                return pad;

        return null;
    }

    /// <summary>Whether a point is on the building toggle.</summary>
    public bool HitsToggle(double x, double y) => Toggle.Contains(x, y);
}
