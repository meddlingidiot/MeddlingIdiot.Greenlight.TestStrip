using Greenlight.TestStrip;

namespace Greenlight.TestStrip.UnitTests;

/// <summary>
/// Where everything sits, and — the part that actually matters — that the rectangle the canvas
/// draws is the rectangle the window hit-tests. A pad that lights under the pointer but fires
/// its neighbour looks perfectly fine in a screenshot.
/// </summary>
public sealed class StripLayoutTests
{
    [Fact]
    public void Pads_run_left_to_right_in_order_without_overlapping()
    {
        var layout = new StripLayout();
        var previous = layout.Grip;

        foreach (var pad in StripLayout.Order)
        {
            var rect = layout.RectFor(pad);
            Assert.True(rect.X >= previous.Right, $"{pad} overlaps what is to its left.");
            previous = rect;
        }

        Assert.True(layout.Toggle.X >= previous.Right);
    }

    [Fact]
    public void Everything_fits_inside_the_strip()
    {
        var layout = new StripLayout();

        foreach (var pad in StripLayout.Order)
        {
            var rect = layout.RectFor(pad);
            Assert.InRange(rect.X, 0, layout.Width);
            Assert.InRange(rect.Right, 0, layout.Width);
            Assert.InRange(rect.Y, 0, layout.Height);
            Assert.InRange(rect.Bottom, 0, layout.Height);
        }

        Assert.InRange(layout.Toggle.Right, 0, layout.Width);
    }

    [Fact]
    public void The_centre_of_every_pad_hit_tests_as_that_pad()
    {
        var layout = new StripLayout();

        foreach (var pad in StripLayout.Order)
        {
            var rect = layout.RectFor(pad);
            Assert.Equal(pad, layout.HitTest(rect.CentreX, rect.CentreY));
        }
    }

    [Fact]
    public void The_centre_of_the_toggle_hits_the_toggle_and_no_pad()
    {
        var layout = new StripLayout();

        Assert.True(layout.HitsToggle(layout.Toggle.CentreX, layout.Toggle.CentreY));
        Assert.Null(layout.HitTest(layout.Toggle.CentreX, layout.Toggle.CentreY));
    }

    [Fact]
    public void The_grip_is_not_a_button_so_a_drag_starts_there()
    {
        var layout = new StripLayout();

        Assert.Null(layout.HitTest(layout.Grip.CentreX, layout.Grip.CentreY));
        Assert.False(layout.HitsToggle(layout.Grip.CentreX, layout.Grip.CentreY));
    }

    [Fact]
    public void The_gaps_between_pads_are_chassis_and_so_drag_the_window()
    {
        var layout = new StripLayout();
        var green = layout.RectFor(Pad.Green);
        var amber = layout.RectFor(Pad.Amber);

        var between = (green.Right + amber.X) / 2;
        Assert.Null(layout.HitTest(between, green.CentreY));
    }

    [Fact]
    public void Above_and_below_the_pads_is_chassis_too()
    {
        var layout = new StripLayout();
        var red = layout.RectFor(Pad.Red);

        Assert.Null(layout.HitTest(red.CentreX, red.Y - 2));
        Assert.Null(layout.HitTest(red.CentreX, red.Bottom + 2));
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(1.0)]
    [InlineData(1.8)]
    public void Hit_testing_follows_the_scale(double scale)
    {
        var layout = new StripLayout(scale);

        foreach (var pad in StripLayout.Order)
        {
            var rect = layout.RectFor(pad);
            Assert.Equal(pad, layout.HitTest(rect.CentreX, rect.CentreY));
        }
    }

    [Fact]
    public void Scaling_up_makes_the_whole_strip_bigger()
    {
        var ordinary = new StripLayout();
        var large = new StripLayout(1.8);

        Assert.True(large.Width > ordinary.Width);
        Assert.True(large.Height > ordinary.Height);
    }

    [Fact]
    public void A_silly_scale_is_clamped_rather_than_believed()
    {
        // The file is hand-editable. A scale of 40 should give somebody a big strip, not a window
        // the size of four desktops that cannot be dragged back.
        Assert.Equal(3.0, new StripLayout(40).Scale);
        Assert.Equal(0.6, new StripLayout(0.01).Scale);
    }

    [Fact]
    public void A_point_outside_the_strip_hits_nothing()
    {
        var layout = new StripLayout();

        Assert.Null(layout.HitTest(-5, 10));
        Assert.Null(layout.HitTest(layout.Width + 5, 10));
        Assert.Null(layout.HitTest(10, layout.Height + 5));
    }
}
