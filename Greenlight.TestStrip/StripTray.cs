using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;
using Greenlight.Sdk;

namespace Greenlight.TestStrip;

/// <summary>
/// The mascot in the notification area: somewhere to say what is being held, and the only way
/// back to a strip that has been hidden.
/// </summary>
/// <remarks>
/// The strip has no title bar, and its close button hides rather than quits: the hold lives as
/// long as the process does, so a close that ended it would drop somebody's red light halfway
/// through the thing they were testing. Quitting is here instead, where it takes a deliberate
/// trip to find — and where it is nowhere near the RED pad people are actually aiming at.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class StripTray : IDisposable
{
    private static readonly Uri IconUri = new("avares://Greenlight.TestStrip/Assets/MeddlingIdiot.ico");

    private readonly StripConfig _config;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _holding;
    private readonly NativeMenuItem _reason;
    private readonly NativeMenuItem _showing;

    public StripTray(StripConfig config)
    {
        _config = config;

        _holding = new NativeMenuItem { Header = "Waiting for Greenlight…", IsEnabled = false };

        // Greenlight's own account of why it is the colour it is. With a hold in force this reads
        // back as a drill naming this app, which is the confirmation that the hold actually landed
        // rather than merely being accepted.
        _reason = new NativeMenuItem { Header = "—", IsEnabled = false };

        _showing = new NativeMenuItem
        {
            Header = "Show the strip",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = true,
        };
        _showing.Click += (_, _) => SetShowing(!IsShowing?.Invoke() ?? true);

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(IconUri)),
            ToolTipText = "Greenlight test strip",
            Menu = BuildMenu(),
            IsVisible = true,
        };

        _tray.Clicked += (_, _) => SetShowing(!IsShowing?.Invoke() ?? true);
    }

    /// <summary>Whether the strip is on screen.</summary>
    public Func<bool>? IsShowing { get; set; }

    /// <summary>Put the strip on screen, or take it away.</summary>
    public Action<bool>? OnSetShowing { get; set; }

    /// <summary>A setting changed that the strip can absorb where it stands.</summary>
    public Action? OnConfigChanged { get; set; }

    /// <summary>Re-read the file, for colours changed by hand.</summary>
    public Action? OnReloadConfig { get; set; }

    /// <summary>Hand the indicators back, without going to find the strip.</summary>
    public Action? OnRelease { get; set; }

    public Action? OnQuit { get; set; }

    /// <summary>Say what is being claimed, in the tooltip and at the top of the menu.</summary>
    public void Show(Pad held, bool building, GreenlightAvailability availability)
    {
        if (availability != GreenlightAvailability.Connected)
        {
            _holding.Header = availability switch
            {
                GreenlightAvailability.Disabled => "Greenlight has its local API switched off",
                GreenlightAvailability.Incompatible => "Greenlight speaks a protocol this build does not",
                _ => "No Greenlight attached — nothing to hold",
            };

            _tray.ToolTipText = "Greenlight test strip — not attached";
            _showing.IsChecked = IsShowing?.Invoke() ?? true;
            return;
        }

        var claim = held switch
        {
            Pad.Green => "Holding every indicator green",
            Pad.Amber => "Holding every indicator yellow",
            Pad.Red => "Holding every indicator red",
            Pad.Off => "Holding every indicator grey",
            _ => "Live — showing the real build state",
        };

        _holding.Header = building && held != Pad.Live ? claim + ", and pretending a build is running" : claim;
        _tray.ToolTipText = $"Greenlight test strip — {Short(held)}{(building ? ", building" : string.Empty)}";
        _showing.IsChecked = IsShowing?.Invoke() ?? true;
    }

    /// <summary>Greenlight's own reason for its current colour, as it comes back.</summary>
    public void ShowReason(string reason) =>
        _reason.Header = string.IsNullOrWhiteSpace(reason) ? "—" : Trim(reason);

    /// <summary>
    /// A command Greenlight said no to. Almost always the *allow commands* setting being off,
    /// which is a thing the user can fix and would otherwise look like a broken strip.
    /// </summary>
    public void ShowRefusal(CommandResult result) =>
        _holding.Header = result.Outcome switch
        {
            CommandOutcome.Refused => $"Greenlight refused it: {result.Message ?? result.Error ?? "no reason given"}",
            CommandOutcome.TimedOut => "Greenlight did not answer in time",
            _ => "No Greenlight attached — nothing to hold",
        };

    public void Dispose()
    {
        _tray.IsVisible = false;
        _tray.Dispose();
    }

    private static string Short(Pad held) => held switch
    {
        Pad.Green => "green",
        Pad.Amber => "yellow",
        Pad.Red => "red",
        Pad.Off => "grey",
        _ => "live",
    };

    /// <summary>Keep a long reason from stretching the menu across the screen.</summary>
    private static string Trim(string reason) =>
        reason.Length <= 60 ? reason : reason[..57] + "…";

    private void SetShowing(bool showing)
    {
        OnSetShowing?.Invoke(showing);
        _showing.IsChecked = showing;
    }

    private NativeMenu BuildMenu() =>
    [
        _holding,
        _reason,
        new NativeMenuItemSeparator(),
        _showing,
        Item("Hand the indicators back", () => OnRelease?.Invoke()),
        new NativeMenuItemSeparator(),
        Submenu("How big",
            Scale("Small", 0.8),
            Scale("Ordinary", 1.0),
            Scale("Large", 1.3),
            Scale("Across the room", 1.8)),
        Submenu("How solid",
            Opacity("Solid", 1.0),
            Opacity("Nearly solid", 0.97),
            Opacity("Half there", 0.6)),
        Check("Stay on top", () => _config.Topmost, value =>
        {
            _config.Topmost = value;
            _config.Save();
            OnConfigChanged?.Invoke();
        }),
        new NativeMenuItemSeparator(),
        Item("Edit the colours…", EditConfig),
        Item("Reload the file", () => OnReloadConfig?.Invoke()),
        new NativeMenuItemSeparator(),
        Item("Quit", () => OnQuit?.Invoke()),
    ];

    private NativeMenuItem Scale(string header, double scale) =>
        Choice(header, () => Math.Abs(_config.Scale - scale) < 0.001, () =>
        {
            _config.Scale = scale;
            _config.Save();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Opacity(string header, double opacity) =>
        Choice(header, () => Math.Abs(_config.Opacity - opacity) < 0.001, () =>
        {
            _config.Opacity = opacity;
            _config.Save();
            OnConfigChanged?.Invoke();
        });

    // ── Menu plumbing ─────────────────────────────────────────────────────────
    // Lifted wholesale from the other clients in the family: each option asks the config what it
    // should look like as the menu opens, rather than remembering what it last wrote. The file is
    // hand-editable and reloadable from this menu, so anything keeping its own state starts lying
    // the moment it is.

    private static NativeMenuItem Check(string header, Func<bool> isOn, Action<bool> set)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isOn(),
        };

        item.Click += (_, _) =>
        {
            set(!isOn());
            item.IsChecked = isOn();
        };

        return item;
    }

    private static NativeMenuItem Item(string header, Action click)
    {
        var item = new NativeMenuItem { Header = header };
        item.Click += (_, _) => click();
        return item;
    }

    private static NativeMenuItem Submenu(string header, params NativeMenuItem[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items) menu.Add(item);

        void Retick()
        {
            foreach (var item in items)
                if (item.CommandParameter is Func<bool> isChosen)
                    item.IsChecked = isChosen();
        }

        foreach (var item in items) item.Click += (_, _) => Retick();
        menu.Opening += (_, _) => Retick();

        return new NativeMenuItem { Header = header, Menu = menu };
    }

    private static NativeMenuItem Choice(string header, Func<bool> isChosen, Action choose)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = isChosen(),
            CommandParameter = isChosen,
        };

        item.Click += (_, _) => choose();
        return item;
    }

    /// <summary>Open <c>teststrip.json</c> in whatever the machine opens JSON with.</summary>
    private void EditConfig()
    {
        try
        {
            if (!File.Exists(StripConfig.DefaultPath)) _config.Save();
            Process.Start(new ProcessStartInfo(StripConfig.DefaultPath) { UseShellExecute = true });
        }
        catch
        {
            // No editor associated with .json, or the shell refused. Not worth interrupting anyone over.
        }
    }
}
