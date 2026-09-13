using Greenlight.TestStrip;

namespace Greenlight.TestStrip.UnitTests;

/// <summary>
/// What is lit, and how hard it is breathing.
/// </summary>
/// <remarks>
/// The cases worth having are the ones nobody would catch by looking at it: a pad that keeps a
/// sliver of glow after being released, a breath that never comes back down after the building
/// toggle goes off, a changeover that leaves two pads lit forever.
/// </remarks>
public sealed class StripSceneTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(33);

    /// <summary>Run the scene for a while, a frame at a time, the way the window does.</summary>
    private static void Run(StripScene scene, TimeSpan duration)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += Frame) scene.Advance(Frame);
    }

    private static PadFrame PadOf(StripFrame frame, Pad pad) => pad switch
    {
        Pad.Live => frame.Live,
        Pad.Off => frame.Off,
        Pad.Green => frame.Green,
        Pad.Amber => frame.Amber,
        _ => frame.Red,
    };

    [Fact]
    public void Starts_live_with_nothing_glowing()
    {
        var frame = new StripScene().Frame();

        Assert.Equal(0, frame.Live.Glow);
        Assert.Equal(0, frame.Green.Glow);
        Assert.Equal(0, frame.Amber.Glow);
        Assert.Equal(0, frame.Red.Glow);
        Assert.Equal(0, frame.Off.Glow);
    }

    [Fact]
    public void Starts_fully_dimmed_because_nothing_is_attached()
    {
        Assert.Equal(1, new StripScene().Frame().Dimmed);
    }

    [Theory]
    [InlineData(Pad.Green)]
    [InlineData(Pad.Amber)]
    [InlineData(Pad.Red)]
    [InlineData(Pad.Off)]
    public void Held_pad_is_the_only_one_glowing(Pad held)
    {
        var scene = new StripScene { Connected = true, Held = held };
        Run(scene, TimeSpan.FromSeconds(1));

        var frame = scene.Frame();
        Assert.True(PadOf(frame, held).Glow > 0);

        foreach (var other in StripLayout.Order)
        {
            if (other == held) continue;
            Assert.Equal(0, PadOf(frame, other).Glow);
        }
    }

    [Fact]
    public void Releasing_puts_every_glow_out()
    {
        var scene = new StripScene { Connected = true, Held = Pad.Red };
        Run(scene, TimeSpan.FromSeconds(1));

        scene.Held = Pad.Live;
        Run(scene, TimeSpan.FromSeconds(1));

        var frame = scene.Frame();
        foreach (var pad in StripLayout.Order) Assert.Equal(0, PadOf(frame, pad).Glow);
    }

    [Fact]
    public void Live_pad_fills_but_never_breathes()
    {
        var scene = new StripScene { Connected = true };
        Run(scene, TimeSpan.FromSeconds(2));

        var frame = scene.Frame();

        // It is a state, not a light: lit enough to read as the one in force, with no pulse to
        // suggest it is claiming something.
        Assert.Equal(0, frame.Live.Glow);
        Assert.True(frame.Live.Fill > frame.Red.Fill);
    }

    [Fact]
    public void Changeover_hands_the_glow_over_rather_than_blinking_dark()
    {
        var scene = new StripScene { Connected = true, Held = Pad.Green };
        Run(scene, TimeSpan.FromSeconds(2));

        scene.Held = Pad.Red;
        scene.Advance(TimeSpan.FromMilliseconds(80));   // mid-changeover

        var frame = scene.Frame();
        Assert.True(frame.Green.Fill > 0.3);
        Assert.True(frame.Red.Fill > 0.3);
    }

    [Fact]
    public void Changeover_finishes()
    {
        var scene = new StripScene { Connected = true, Held = Pad.Green };
        Run(scene, TimeSpan.FromSeconds(1));

        scene.Held = Pad.Red;
        Run(scene, TimeSpan.FromSeconds(1));

        // The pad that was lit has to end up indistinguishable from one that never was, or the
        // strip slowly accumulates ghosts of everything it has ever held.
        Assert.Equal(0, scene.Frame().Green.Glow);
        Assert.Equal(scene.Frame().Amber.Fill, scene.Frame().Green.Fill, 3);
    }

    [Fact]
    public void Red_breathes_faster_than_green()
    {
        Assert.True(Swings(Pad.Red) > Swings(Pad.Green));

        // Count how many times the glow crests in ten seconds.
        static int Swings(Pad pad)
        {
            var scene = new StripScene { Connected = true, Held = pad };
            Run(scene, TimeSpan.FromSeconds(1));   // let the changeover settle

            var crests = 0;
            var rising = true;
            var last = PadOf(scene.Frame(), pad).Glow;

            for (var i = 0; i < 300; i++)
            {
                scene.Advance(Frame);
                var now = PadOf(scene.Frame(), pad).Glow;

                if (rising && now < last) { crests++; rising = false; }
                else if (!rising && now > last) rising = true;

                last = now;
            }

            return crests;
        }
    }

    [Fact]
    public void Building_makes_the_same_pad_breathe_faster()
    {
        Assert.True(Crests(building: true) > Crests(building: false));

        static int Crests(bool building)
        {
            var scene = new StripScene { Connected = true, Held = Pad.Amber, IsBuilding = building };
            Run(scene, TimeSpan.FromSeconds(1));

            var crests = 0;
            var rising = true;
            var last = scene.Frame().Amber.Glow;

            for (var i = 0; i < 300; i++)
            {
                scene.Advance(Frame);
                var now = scene.Frame().Amber.Glow;

                if (rising && now < last) { crests++; rising = false; }
                else if (!rising && now > last) rising = true;

                last = now;
            }

            return crests;
        }
    }

    [Fact]
    public void Building_toggle_lights_with_the_held_pad_and_goes_out_with_it()
    {
        var scene = new StripScene { Connected = true, Held = Pad.Red, IsBuilding = true };
        Run(scene, TimeSpan.FromSeconds(1));

        var lit = scene.Frame().Building;

        scene.IsBuilding = false;
        Run(scene, TimeSpan.FromSeconds(1));

        Assert.True(lit > scene.Frame().Building);
    }

    [Fact]
    public void Glow_stays_inside_its_range_however_long_a_frame_took()
    {
        var scene = new StripScene { Connected = true, Held = Pad.Red, IsBuilding = true };

        // A stalled UI thread hands the scene seconds rather than milliseconds. The phase has to
        // wrap rather than run away, or the glow comes back as something a brush cannot take.
        foreach (var seconds in new[] { 0.016, 4.0, 0.033, 90.0, 0.5 })
        {
            scene.Advance(TimeSpan.FromSeconds(seconds));

            var frame = scene.Frame();
            Assert.InRange(frame.Red.Glow, 0, 1);
            Assert.InRange(frame.Red.Fill, 0, 1);
            Assert.InRange(frame.Building, 0, 1);
        }
    }

    [Fact]
    public void Negative_and_zero_frames_change_nothing()
    {
        var scene = new StripScene { Connected = true, Held = Pad.Green };
        Run(scene, TimeSpan.FromSeconds(1));

        var before = scene.Frame();
        scene.Advance(TimeSpan.Zero);
        scene.Advance(TimeSpan.FromSeconds(-5));

        Assert.Equal(before, scene.Frame());
    }

    [Fact]
    public void Attaching_brings_the_strip_up_out_of_grey()
    {
        var scene = new StripScene();
        Run(scene, TimeSpan.FromSeconds(1));
        Assert.Equal(1, scene.Frame().Dimmed);

        scene.Connected = true;
        Run(scene, TimeSpan.FromSeconds(1));
        Assert.Equal(0, scene.Frame().Dimmed);
    }

    [Fact]
    public void Losing_greenlight_dims_it_again()
    {
        var scene = new StripScene { Connected = true, Held = Pad.Red };
        Run(scene, TimeSpan.FromSeconds(1));

        scene.Connected = false;
        Run(scene, TimeSpan.FromSeconds(1));

        Assert.Equal(1, scene.Frame().Dimmed);
    }

    [Fact]
    public void Setting_the_pad_it_already_holds_does_not_restart_the_breath()
    {
        var scene = new StripScene { Connected = true, Held = Pad.Amber };
        Run(scene, TimeSpan.FromSeconds(2));

        var before = scene.Frame();
        scene.Held = Pad.Amber;

        Assert.Equal(before, scene.Frame());
    }
}
