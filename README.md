# Greenlight Test Strip

A draggable strip of five pads and a toggle. Press one and every indicator Greenlight drives —
its own tray icon, the desktop stoplight, the hardware lamps and every attached app — holds that
colour until you let go of it.

It is how you find out whether a client actually goes red, without breaking a build to ask.

```
 ⣿  LIVE   GREY   GREEN   AMBER   RED    BUILDING
```

- **LIVE** hands the indicators back to the real build state.
- **GREY**, **GREEN**, **AMBER**, **RED** hold every indicator at that colour.
- **BUILDING** pretends a build is running, so the indicators blink as well as sit.

The held pad glows and breathes. Red breathes faster than amber, amber faster than green, and
all of them faster again while BUILDING is on — so a glance from across the desk tells you how
the strip is set without reading the labels. That is theatre and nothing else: none of it is
anything Greenlight said.

Drag it by the grip, or by any part of the chassis that is not a pad. It remembers where you
left it.

## The one thing to know

**The hold lives and dies with the process.** Greenlight releases it the moment the client that
placed it detaches — which is what stops a crashed tester leaving your light lying to you all
afternoon, and it means the strip has to stay open for the hold to stay in force. There is no
fire-and-forget version of this; a build that set a colour and exited would appear to do nothing
at all.

Quitting releases the hold on the way out. So does pulling the plug on Greenlight, and so does
pressing the lit pad a second time.

## What it is made of

The whole Greenlight integration is one call:

```csharp
await greenlight.HoldIndicatorsAsync(GreenlightStatus.Red, building: true);
```

Everything else is Avalonia drawing, a tray icon, and a JSON file. It takes the SDK as a package
the same way your own app would:

```
dotnet add package MeddlingIdiot.Greenlight.Sdk
```

`StripScene` decides what is lit and how hard it is breathing; `StripLayout` decides where each
pad sits and answers the hit-test. Neither touches Avalonia, so both are covered by tests — this
is the instrument the other clients are checked with, and one whose pads fire their neighbour or
whose pulse sticks would be worse than no instrument at all.

## Settings

`%AppData%\Greenlight.TestStrip\teststrip.json`, written out with the defaults on first run.
Colours, scale, opacity, always-on-top and the last position. Most of it is on the tray menu as
well; the colours are not, because four hex strings are not something anyone wants to pick off a
menu.

## Running it

```bash
dotnet run --project Greenlight.TestStrip
```

Windows only — the tray icon and the screen geometry are Win32. The SDK itself is plain
net8.0/net10.0.

If nothing lights up, Greenlight has commands switched off: the strip says so at the top of its
tray menu rather than just looking broken.
