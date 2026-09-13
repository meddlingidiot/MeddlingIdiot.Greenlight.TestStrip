using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenlight.TestStrip;

/// <summary>
/// The strip's colours, size and where it was left, read from a JSON file the user can edit.
/// Written out with the defaults the first time it is missing.
/// </summary>
/// <remarks>
/// In AppData rather than beside the executable, which lives under <c>bin</c> and is fair game
/// for a rebuild. The position matters more here than the colours do: this window is dragged
/// somewhere out of the way of whatever is being tested, and having to drag it back after every
/// restart would be the thing that stops it being reached for.
/// </remarks>
public sealed class StripConfig
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Greenlight.TestStrip", "teststrip.json");

    // ── colours ───────────────────────────────────────────────────────────────
    // The same four Greenlight itself wears, so a pad held next to a client under test is the
    // same colour the client should be going.

    /// <summary>The release pad: cool and unlit, because it is the absence of a claim.</summary>
    public string Live { get; set; } = "#7C8896";

    /// <summary>Greenlight's grey.</summary>
    public string Off { get; set; } = "#6A7079";

    public string Green { get; set; } = "#4BFF86";
    public string Amber { get; set; } = "#FFCE42";
    public string Red { get; set; } = "#FF4E3C";

    /// <summary>The building toggle, which is not a status and so is not one of the four.</summary>
    public string Building { get; set; } = "#4FC3F7";

    /// <summary>The chassis behind the pads.</summary>
    public string Chassis { get; set; } = "#F21C2128";

    /// <summary>Everything scaled at once, for a strip that is too small on a big display.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>How solid the whole window is.</summary>
    public double Opacity { get; set; } = 0.97;

    /// <summary>Stay above other windows. On, because the point of it is to sit over what it is testing.</summary>
    public bool Topmost { get; set; } = true;

    /// <summary>
    /// Where it was last dragged to, in physical pixels. Null until it has been moved, which is
    /// what puts the first run in the middle of the screen rather than at 0,0.
    /// </summary>
    public int? X { get; set; }

    public int? Y { get; set; }

    /// <summary>The colour for a pad. What the canvas asks, every frame.</summary>
    public string ColourFor(Pad pad) => pad switch
    {
        Pad.Green => Green,
        Pad.Amber => Amber,
        Pad.Red => Red,
        Pad.Off => Off,
        _ => Live,
    };

    /// <summary>
    /// Load the file, writing the defaults out first if it is not there. A file that cannot be
    /// read falls back to the defaults rather than refusing to start — a stray comma should not
    /// cost you the tool you reached for to debug something else.
    /// </summary>
    public static StripConfig Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
            {
                var fresh = new StripConfig();
                fresh.Save(file);
                return fresh;
            }

            var loaded = JsonSerializer.Deserialize<StripConfig>(File.ReadAllText(file), Json);
            if (loaded is null) return new StripConfig();

            // A file written before one of these existed deserializes it as null, and so does a
            // hand edit that deleted a line.
            var defaults = new StripConfig();
            loaded.Live ??= defaults.Live;
            loaded.Off ??= defaults.Off;
            loaded.Green ??= defaults.Green;
            loaded.Amber ??= defaults.Amber;
            loaded.Red ??= defaults.Red;
            loaded.Building ??= defaults.Building;
            loaded.Chassis ??= defaults.Chassis;

            loaded.Scale = Math.Clamp(loaded.Scale, 0.6, 3.0);
            loaded.Opacity = Math.Clamp(loaded.Opacity, 0.2, 1.0);

            return loaded;
        }
        catch
        {
            return new StripConfig();
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // A tool that cannot write its config still runs perfectly well on the defaults.
        }
    }

    /// <summary>Take on everything from a freshly-read file, in place, because the tray is holding this one.</summary>
    public void CopyFrom(StripConfig other)
    {
        Live = other.Live;
        Off = other.Off;
        Green = other.Green;
        Amber = other.Amber;
        Red = other.Red;
        Building = other.Building;
        Chassis = other.Chassis;
        Scale = other.Scale;
        Opacity = other.Opacity;
        Topmost = other.Topmost;
        X = other.X;
        Y = other.Y;
    }
}
