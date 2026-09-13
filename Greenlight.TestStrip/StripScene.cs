namespace Greenlight.TestStrip;

/// <summary>
/// One of the things the strip can claim. Deliberately its own enum rather than the SDK's
/// <c>GreenlightStatus</c>: this is what a <em>pad</em> is, and the pads include a release
/// that is not a status at all.
/// </summary>
public enum Pad
{
    /// <summary>Hand the indicators back to the real build state. Not a colour.</summary>
    Live,

    /// <summary>Greenlight's grey — nothing known.</summary>
    Off,
    Green,
    Amber,
    Red,
}

/// <summary>How one pad should be drawn this frame.</summary>
/// <param name="Fill">How solid the pad's own colour is, 0–1. Unheld pads sit low but never at zero.</param>
/// <param name="Glow">How far the bloom reaches past the pad, 0–1. Zero on everything but the held pad.</param>
public readonly record struct PadFrame(double Fill, double Glow);

/// <summary>Everything the canvas needs to draw one frame of the strip.</summary>
/// <param name="Live">The release pad.</param>
/// <param name="Off">The grey pad.</param>
/// <param name="Green">The green pad.</param>
/// <param name="Amber">The amber pad.</param>
/// <param name="Red">The red pad.</param>
/// <param name="Building">
/// How lit the building toggle is, 0–1. Pulses in step with the held pad when it is on, so the
/// two read as one instrument rather than two.
/// </param>
/// <param name="Dimmed">
/// How far the whole strip is faded towards grey, 0–1, because there is no Greenlight attached
/// to hold anything. One at a cold start.
/// </param>
public readonly record struct StripFrame(
    PadFrame Live,
    PadFrame Off,
    PadFrame Green,
    PadFrame Amber,
    PadFrame Red,
    double Building,
    double Dimmed);

/// <summary>
/// What the strip is holding and how hard it is breathing, with no drawing in it.
/// </summary>
/// <remarks>
/// <para>
/// Free of Avalonia so it can be tested, which matters more here than in the desk toys: this
/// one is the instrument you reach for when you do not trust what a client is showing you, and
/// an instrument whose own pulse drifts or sticks is worse than no instrument. A pad that kept
/// a sliver of glow after being released, or a breath that never came back down after the
/// building toggle went off, is exactly the kind of thing nobody catches by looking.
/// </para>
/// <para>
/// The pulse rate is the one piece of deliberate theatre: red breathes faster than amber, amber
/// faster than green. It says nothing Greenlight said — it is there so that a glance at the
/// strip from across the desk tells you which way it is set without reading the labels.
/// </para>
/// </remarks>
public sealed class StripScene
{
    /// <summary>How long a pad takes to take over the glow from the one before it.</summary>
    private static readonly TimeSpan Changeover = TimeSpan.FromSeconds(0.22);

    /// <summary>How long the strip takes to come up out of grey when Greenlight turns up, and to fall back.</summary>
    private static readonly TimeSpan DimFade = TimeSpan.FromSeconds(0.4);

    /// <summary>One full breath of the held pad at rest, per status.</summary>
    /// <remarks>Red is the quick one. See the class remarks — it is theatre, and on purpose.</remarks>
    private static readonly TimeSpan GreenBreath = TimeSpan.FromSeconds(2.6);
    private static readonly TimeSpan AmberBreath = TimeSpan.FromSeconds(1.8);
    private static readonly TimeSpan RedBreath = TimeSpan.FromSeconds(1.1);
    private static readonly TimeSpan OffBreath = TimeSpan.FromSeconds(3.4);

    /// <summary>How much faster the same breath goes while the building toggle is on.</summary>
    private const double BuildingRush = 2.0;

    /// <summary>How far the glow swings between the bottom and the top of a breath.</summary>
    private const double BreathDepth = 0.45;

    /// <summary>What an unheld pad keeps, so the strip still reads as five buttons in the dark.</summary>
    private const double RestingFill = 0.22;

    private Pad _held = Pad.Live;
    private Pad _leaving = Pad.Live;

    private double _phase;        // where in the breath, in turns
    private double _handover = 1; // 0 at the moment of a change, 1 once the new pad owns the glow
    private double _dim = 1;      // 1 when there is no Greenlight to talk to

    /// <summary>Which pad is lit. Setting it starts a changeover; the old pad's glow falls away.</summary>
    public Pad Held
    {
        get => _held;
        set
        {
            if (_held == value) return;

            _leaving = _held;
            _held = value;
            _handover = 0;

            // Start the new pad at the bottom of its breath rather than wherever the last one
            // happened to be. Taking over mid-swell reads as a flicker, not a change.
            _phase = 0;
        }
    }

    /// <summary>Whether the strip is also claiming a build is running.</summary>
    public bool IsBuilding { get; set; }

    /// <summary>Whether there is a Greenlight attached to hold anything at all.</summary>
    public bool Connected { get; set; }

    /// <summary>Move the breath and the changeover on by one frame.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero) return;

        var breath = Breath(_held);
        var rate = IsBuilding ? BuildingRush : 1.0;
        _phase = Wrap(_phase + elapsed.TotalSeconds / breath.TotalSeconds * rate);

        _handover = Approach(_handover, 1, elapsed, Changeover);
        _dim = Approach(_dim, Connected ? 0 : 1, elapsed, DimFade);
    }

    /// <summary>What to draw right now.</summary>
    public StripFrame Frame()
    {
        // A cosine off the phase: starts at the bottom of the breath, rises to the top and back
        // with no corner at either end. Sine would start halfway up and jump on a changeover.
        var swell = (1 - Math.Cos(_phase * 2 * Math.PI)) / 2;
        var lit = 1 - BreathDepth + BreathDepth * swell;

        return new StripFrame(
            PadOf(Pad.Live, lit),
            PadOf(Pad.Off, lit),
            PadOf(Pad.Green, lit),
            PadOf(Pad.Amber, lit),
            PadOf(Pad.Red, lit),

            // The toggle borrows the held pad's breath rather than running one of its own, so the
            // strip pulses as a single object. Off, it sits at the resting fill like any unheld pad.
            IsBuilding ? lit : RestingFill,
            _dim);
    }

    private PadFrame PadOf(Pad pad, double lit)
    {
        // Two pads can be alight at once, mid-changeover: the new one coming up and the old one
        // going down. Anything else and the strip blinks dark between two settings.
        var share = pad == _held ? _handover
            : pad == _leaving ? 1 - _handover
            : 0;

        if (share <= 0) return new PadFrame(RestingFill, 0);

        // The release pad is a state, not a light. It fills when it is the one in force but never
        // breathes — nothing is being claimed, and a pulsing "live" would say the opposite.
        var glow = pad == Pad.Live ? 0 : lit * share;

        return new PadFrame(RestingFill + (1 - RestingFill) * share, glow);
    }

    private static TimeSpan Breath(Pad pad) => pad switch
    {
        Pad.Red => RedBreath,
        Pad.Amber => AmberBreath,
        Pad.Green => GreenBreath,
        _ => OffBreath,
    };

    /// <summary>Move <paramref name="value"/> towards <paramref name="target"/>, taking <paramref name="over"/> to cross the whole range.</summary>
    private static double Approach(double value, double target, TimeSpan elapsed, TimeSpan over)
    {
        var step = elapsed.TotalSeconds / over.TotalSeconds;
        return value < target
            ? Math.Min(target, value + step)
            : Math.Max(target, value - step);
    }

    /// <summary>Keep the phase in 0–1 however long a frame took — a stalled UI thread can hand us seconds.</summary>
    private static double Wrap(double turns) => turns - Math.Floor(turns);
}
