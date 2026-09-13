using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Greenlight.TestStrip;

/// <summary>
/// The strip itself: a chassis, a grip, five pads and a toggle, drawn by hand.
/// </summary>
/// <remarks>
/// <para>
/// Drawn rather than built out of controls with styles. The glow is the whole look, and a bloom
/// that reaches past its own pad and sits under its neighbours is not something a
/// <c>BoxShadow</c> on a <c>Button</c> does convincingly — it clips at the control's bounds,
/// and the pads are close enough together that the clipping is what you notice.
/// </para>
/// <para>
/// The bloom is a stack of rounded rectangles growing outward with the alpha falling off as a
/// square, which is cheap and reads as light. A real blur would be a render-target effect per
/// frame at 60fps for an instrument that spends most of its life showing one steady colour.
/// </para>
/// </remarks>
public sealed class StripCanvas(StripScene scene, StripConfig config, StripLayout layout) : Control
{
    /// <summary>How many rings make up the bloom. Enough to read as a gradient, few enough to be free.</summary>
    private const int BloomRings = 7;

    /// <summary>The corner radius of a pad, before scaling.</summary>
    private const double PadCorner = 9;

    private static readonly Typeface Label = new("Segoe UI Semibold");

    public override void Render(DrawingContext context)
    {
        var frame = scene.Frame();
        var scale = layout.Scale;

        DrawChassis(context);
        DrawGrip(context, frame.Dimmed);

        foreach (var pad in StripLayout.Order)
        {
            var padFrame = pad switch
            {
                Pad.Live => frame.Live,
                Pad.Off => frame.Off,
                Pad.Green => frame.Green,
                Pad.Amber => frame.Amber,
                _ => frame.Red,
            };

            DrawPad(context, layout.RectFor(pad), Colour(config.ColourFor(pad)), padFrame, frame.Dimmed, Caption(pad), scale);
        }

        // The toggle gets no glow of its own — it borrows the held pad's breath through its fill.
        // Two things blooming at once on a strip this size reads as a fault rather than a state.
        DrawPad(context, layout.Toggle, Colour(config.Building), new PadFrame(frame.Building, 0), frame.Dimmed,
            "BUILDING", scale);
    }

    /// <summary>The rounded slab everything sits on.</summary>
    private void DrawChassis(DrawingContext context)
    {
        var chassis = Colour(config.Chassis);
        var rect = new RoundedRect(new Rect(0, 0, Bounds.Width, Bounds.Height), 14 * layout.Scale);

        // Lit from above, very slightly. A flat slab reads as a rectangle someone forgot to style;
        // the gradient is what makes it read as a physical thing the pads are set into.
        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Lighten(chassis, 0.10), 0),
                    new GradientStop(chassis, 0.55),
                    new GradientStop(Darken(chassis, 0.22), 1),
                },
            },
            null,
            rect);

        // A hairline lip, so the strip has an edge against a dark window behind it. Without it the
        // chassis dissolves into anything dark and the pads look like they are floating loose.
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), 1), rect);
    }

    /// <summary>Two columns of dots on the left, which is what says "this thing moves".</summary>
    private void DrawGrip(DrawingContext context, double dimmed)
    {
        var grip = layout.Grip;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(90 * (1 - dimmed * 0.6)), 255, 255, 255));

        const int rows = 4;
        var radius = 1.6 * layout.Scale;
        var spacingY = grip.Height / (rows + 1);
        var spacingX = grip.Width / 3;

        for (var row = 1; row <= rows; row++)
        for (var column = 1; column <= 2; column++)
            context.DrawEllipse(brush, null,
                new Point(grip.X + spacingX * column, grip.Y + spacingY * row), radius, radius);
    }

    /// <summary>One pad: its bloom, its body, its lip and its caption.</summary>
    private void DrawPad(DrawingContext context, PadRect rect, Color colour, PadFrame pad, double dimmed,
        string caption, double scale)
    {
        // Everything fades towards grey when there is no Greenlight to hold. Desaturating rather
        // than dimming, so an unattached strip is legible but visibly not in charge of anything.
        colour = Desaturate(colour, dimmed);

        var corner = PadCorner * scale;
        var body = new RoundedRect(new Rect(rect.X, rect.Y, rect.Width, rect.Height), corner);

        if (pad.Glow > 0.001) DrawBloom(context, rect, colour, pad.Glow, corner, scale);

        // The body carries the fill as alpha rather than as a darker colour: over a translucent
        // chassis, a dark unheld pad would show the desktop through it at a different tint from
        // its neighbours, and the row would stop looking like one instrument.
        //
        // Top-lit, and the lit pad gets a hotter core than its own colour. A pad filled flat at
        // full alpha is at its most saturated and still looks matte — the thing that reads as
        // "this is emitting light" is the middle being paler than the edges, not brighter.
        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Lighten(colour, 0.30 + 0.35 * pad.Glow), 0),
                    new GradientStop(Lighten(colour, 0.05 + 0.25 * pad.Glow), 0.45),
                    new GradientStop(Darken(colour, 0.18), 1),
                },
                Opacity = pad.Fill,
            },
            null,
            body);

        // A brighter lip on the held pad. This is what makes it read as pressed in rather than
        // merely brighter — the glow alone reads as a hover.
        var lip = (byte)(50 + 150 * pad.Glow);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Lighten(colour, 0.35), lip / 255.0), 1.2 * scale), body);

        // A specular line just inside the top edge. One hairline, and the pad stops being a
        // rectangle of colour and starts being a lens with something behind it.
        var sheen = 0.12 + 0.30 * pad.Glow;
        var inset = 2.0 * scale;
        context.DrawLine(
            new Pen(new SolidColorBrush(Colors.White, sheen), 1 * scale),
            new Point(rect.X + corner * 0.7, rect.Y + inset),
            new Point(rect.Right - corner * 0.7, rect.Y + inset));

        DrawCaption(context, rect, caption, pad, dimmed, scale);
    }

    /// <summary>The light spilling out past the pad, as rings of falling alpha.</summary>
    private static void DrawBloom(DrawingContext context, PadRect rect, Color colour, double glow, double corner,
        double scale)
    {
        for (var ring = BloomRings; ring >= 1; ring--)
        {
            var spread = StripLayout.GlowReach * scale * ring / BloomRings;

            // Falls off as a square, which is what light does and, more to the point, what stops
            // the outermost ring drawing a visible hard edge around the bloom.
            var falloff = 1 - (double)ring / (BloomRings + 1);
            var alpha = glow * falloff * falloff * 0.8;

            context.DrawRectangle(
                new SolidColorBrush(colour, alpha),
                null,
                new RoundedRect(
                    new Rect(rect.X - spread, rect.Y - spread, rect.Width + spread * 2, rect.Height + spread * 2),
                    corner + spread));
        }
    }

    private static void DrawCaption(DrawingContext context, PadRect rect, string caption, PadFrame pad, double dimmed,
        double scale)
    {
        // Dark text on a lit pad, pale text on an unlit one. One colour for both would be
        // unreadable at one end or the other, and the held pad is the one that has to be legible
        // from across a desk.
        var ink = pad.Glow > 0.35
            ? Color.FromArgb(235, 12, 14, 18)
            : Color.FromArgb((byte)(170 * (1 - dimmed * 0.45)), 236, 240, 245);

        var text = new FormattedText(
            caption,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Label,
            9.5 * scale,
            new SolidColorBrush(ink));

        context.DrawText(text, new Point(
            rect.CentreX - text.Width / 2,
            rect.CentreY - text.Height / 2));
    }

    private static string Caption(Pad pad) => pad switch
    {
        Pad.Live => "LIVE",
        Pad.Off => "GREY",
        Pad.Green => "GREEN",
        Pad.Amber => "AMBER",
        _ => "RED",
    };

    /// <summary>Pull a colour towards the grey of an unattached strip.</summary>
    private static Color Desaturate(Color colour, double amount)
    {
        if (amount <= 0) return colour;

        // Perceptual weights rather than a flat third each: a flat average turns the amber pad
        // darker than the green one, and the row stops looking evenly lit.
        var grey = (byte)(colour.R * 0.299 + colour.G * 0.587 + colour.B * 0.114);

        return Color.FromArgb(
            colour.A,
            Mix(colour.R, grey, amount),
            Mix(colour.G, grey, amount),
            Mix(colour.B, grey, amount));
    }

    private static byte Mix(byte from, byte to, double amount) =>
        (byte)Math.Clamp(from + (to - from) * amount, 0, 255);

    /// <summary>Towards white, for the top of a pad and the hot core of a lit one.</summary>
    private static Color Lighten(Color colour, double amount) => Color.FromArgb(
        colour.A, Mix(colour.R, 255, amount), Mix(colour.G, 255, amount), Mix(colour.B, 255, amount));

    /// <summary>Towards black, for the bottom of one.</summary>
    private static Color Darken(Color colour, double amount) => Color.FromArgb(
        colour.A, Mix(colour.R, 0, amount), Mix(colour.G, 0, amount), Mix(colour.B, 0, amount));

    /// <summary>Parse a hex colour from the config, falling back rather than throwing on a bad one.</summary>
    private static Color Colour(string value) =>
        Color.TryParse(value, out var colour) ? colour : Colors.Gray;
}
