using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Greenlight.Sdk;
using Greenlight.Sdk.Protocol;

namespace Greenlight.TestStrip;

/// <summary>
/// The strip's wiring: attach to Greenlight, and turn a pad press into a hold.
/// </summary>
/// <remarks>
/// <para>
/// The other samples in the family read Greenlight and draw what it says. This one writes: it
/// asks Greenlight to hold every indicator it drives — its own tray icon, the desktop
/// stoplight, hardware lamps and every attached app — at a colour of your choosing, so a client
/// can be made to go red without breaking a build to do it.
/// </para>
/// <para>
/// <b>The hold lives and dies with this process.</b> Greenlight releases it when the client that
/// placed it detaches, which is what stops a crashed tester leaving somebody's light lying to
/// them all afternoon. It also means the strip has to stay open for the hold to stay in force —
/// there is no fire-and-forget version of this, and a build of it that set a colour and exited
/// would appear to do nothing at all.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class App : Application
{
    private GreenlightClient? _greenlight;
    private StripWindow? _window;
    private StripTray? _tray;
    private StripConfig _config = new();
    private readonly StripScene _scene = new();

    /// <summary>What the strip is claiming. Held here because the window is closed and reopened from the tray.</summary>
    private Pad _held = Pad.Live;

    private bool _building;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Hiding the strip from the tray closes its only window. On the default shutdown mode
            // that would quit the process — and quitting is what drops the hold.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _config = StripConfig.Load();

            _tray = new StripTray(_config)
            {
                IsShowing = () => _window is not null,
                OnSetShowing = showing =>
                {
                    if (showing) ShowStrip();
                    else HideStrip();
                },
                OnConfigChanged = () => _window?.ApplyConfig(),
                OnReloadConfig = ReloadConfig,
                OnRelease = () => Press(Pad.Live),
                OnQuit = () => desktop.Shutdown(),
            };

            ShowStrip();
            StartWatchingGreenlight();

            desktop.Exit += async (_, _) =>
            {
                _window?.RememberPosition();
                _tray?.Dispose();

                // Politeness rather than necessity: the host drops the hold when this connection
                // goes anyway. Asking first means the light is already right by the time the
                // process is gone, instead of a beat later.
                if (_greenlight is not null)
                {
                    if (_held != Pad.Live) await _greenlight.ReleaseIndicatorsAsync();
                    await _greenlight.DisposeAsync();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartWatchingGreenlight()
    {
        _greenlight = new GreenlightClient();

        // The strip watches its own effect land. Holding red and seeing the snapshot come back red
        // is the difference between "the command was accepted" and "the indicators actually moved",
        // and this tool exists for people who need to know which of those happened.
        _greenlight.AvailabilityChanged += (_, e) => Dispatcher.UIThread.Post(() =>
        {
            var connected = e.Availability == GreenlightAvailability.Connected;
            _scene.Connected = connected;

            // A dropped connection took the hold with it. Showing LIVE again is the honest thing:
            // the pad that was lit is no longer claiming anything.
            if (!connected && _held != Pad.Live)
            {
                _held = Pad.Live;
                _scene.Held = Pad.Live;
                _building = false;
                _scene.IsBuilding = false;
            }

            _tray?.Show(_held, _building, e.Availability);
        });

        _greenlight.Changed += (_, e) => Dispatcher.UIThread.Post(() => _tray?.ShowReason(e.Snapshot.Reason));

        // Not awaited and not guarded: an absent Greenlight is a normal operating mode. The strip
        // sits grey until one turns up, and the pads light the moment it does.
        _ = _greenlight.StartAsync();
    }

    /// <summary>A pad was pressed: hold that colour, or hand the indicators back.</summary>
    private void Press(Pad pad)
    {
        // Pressing the lit pad again releases, so the strip can be cleared without reaching for a
        // different button. Every other press is a change of colour.
        if (pad == _held && pad != Pad.Live) pad = Pad.Live;

        _held = pad;
        _scene.Held = pad;

        // Releasing stops claiming a build too. A strip showing LIVE with BUILDING still lit would
        // be claiming half a thing, and the half it kept is the one nobody would expect.
        if (pad == Pad.Live && _building)
        {
            _building = false;
            _scene.IsBuilding = false;
        }

        _tray?.Show(_held, _building, _greenlight?.Availability ?? GreenlightAvailability.Unavailable);
        _ = SendAsync();
    }

    private void ToggleBuilding()
    {
        _building = !_building;
        _scene.IsBuilding = _building;

        // Claiming a build with no colour held would leave the colour following the real state
        // while the blink followed us. Grey is the neutral thing to pin it to.
        if (_building && _held == Pad.Live)
        {
            _held = Pad.Off;
            _scene.Held = Pad.Off;
        }

        _tray?.Show(_held, _building, _greenlight?.Availability ?? GreenlightAvailability.Unavailable);
        _ = SendAsync();
    }

    /// <summary>Push the current claim to Greenlight.</summary>
    private async Task SendAsync()
    {
        if (_greenlight is null) return;

        var result = _held == Pad.Live
            ? await _greenlight.ReleaseIndicatorsAsync()
            : await _greenlight.HoldIndicatorsAsync(StatusOf(_held), _building);

        // Commands come back with an outcome rather than throwing — an absent Greenlight is normal.
        // A refusal is not: it means the user has commands switched off in Greenlight's settings,
        // and without saying so the strip would just look broken.
        if (!result.IsOk) Dispatcher.UIThread.Post(() => _tray?.ShowRefusal(result));
    }

    private void ShowStrip()
    {
        if (_window is not null) return;

        _window = new StripWindow(_config, _scene)
        {
            OnPadPressed = Press,
            OnToggleBuilding = ToggleBuilding,
        };

        _window.Show();
    }

    private void HideStrip()
    {
        _window?.Close();
        _window = null;
    }

    /// <summary>Re-read the file, for colours changed by hand while this was running.</summary>
    private void ReloadConfig()
    {
        var position = (_config.X, _config.Y);
        _config.CopyFrom(StripConfig.Load());

        // The file on disk has wherever the strip was when it was last saved, which is not
        // necessarily where it is now. Keeping the live position stops a reload teleporting it.
        (_config.X, _config.Y) = position;

        _window?.ApplyConfig();
    }

    private static GreenlightStatus StatusOf(Pad pad) => pad switch
    {
        Pad.Green => GreenlightStatus.Green,
        Pad.Amber => GreenlightStatus.Yellow,
        Pad.Red => GreenlightStatus.Red,
        _ => GreenlightStatus.Unknown,
    };
}
