[Русский](README.md)

# PC Logger

Tracks whether the “N minutes at the PC — N minutes break” regime is being kept,
and builds a local HTML report. Windows, a tray icon, no data leaves the machine.

## What exactly gets recorded

Only the **fact** of input, second by second: whether there was keyboard or mouse
activity, whether there was gamepad activity, which app was in focus. Keystrokes
themselves are never read or stored anywhere, and no low-level keyboard hook is used.

Once every ten seconds this is joined by the list of apps with visible windows — the
“Game” track is built from that.

## Installation

Download `PcLogger-win-x64.zip` from the [releases page](https://github.com/navoznov/pc-logger/releases),
unpack it **whole** into any folder, and run `PcLogger.exe`. No need to install .NET —
the runtime is bundled inside.

The `dashboard` folder next to the exe is only needed if you want to edit how the report
looks: files in it take priority, and a copy of them is baked into the exe, so deleting the folder breaks nothing.

Auto-start at sign-in:

```
PcLogger.exe --install     # registers a task in Task Scheduler
PcLogger.exe --uninstall   # removes it
```

No administrator rights needed: the task is created for the current user. `--install`
has to be run from `PcLogger.exe` itself — through `dotnet run` the command refuses to
work, because Task Scheduler would end up with a path to `dotnet.exe`, not to the program.

The task is registered with no time limit and no stop-on-battery: Task Scheduler's
defaults would kill the collector after three days of continuous running, and on a
laptop would keep it from starting at all.

## Tray menu

| Item | What it does |
|---|---|
| **Report** | Builds the HTML and opens it in the browser |
| **Open data folder** | `%LOCALAPPDATA%\PcLogger` — the database and the config |
| **Game folders** | Opens `config.json` in Notepad |
| **Quit** | Removes the icon and stops recording |

## Game folders

`config.json` in the data folder:

```json
{
  "game_folders": [
    "D:\\Steam\\steamapps\\common",
    "D:\\Games"
  ]
}
```

Any executable inside these folders counts as a game. The list is read **at
report-building time**, not while recording: add a folder, save it, click “Report” —
the game track repaints retroactively across the whole history, with no app restart.

A broken or empty config doesn't block the report: the game track just stays empty,
everything else builds as usual.

## How to read the report

Four tracks above a shared time axis:

```
PC on        ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░
Input        ░▓▓▓▓░░░░▓▓▓▓░░░░▓▓▓▓▓▓░░░░░
Game         ░████▒▒▒▒████▒▒▒▒██████░░░░░    █ in focus · ▒ in the background
Regime       [  ok  ][ break ][  ok  ]
```

- **PC on** — whether anything was recorded at all. Empty means the computer was off
  or asleep, not that nobody was at it.
- **Input** — whether there was input. No input while the PC is on reads as “stepped away”.
- **Game** — context, not an accusation: it never affects the regime verdict.
- **Regime** — blocks and breaks. Blocks that are over the limit are marked with
  hatching, not just colour.

Hover over any track — a vertical line runs through all four, and next to it appears
the time at the cursor and a description of what's under it: which game was in focus,
whether there was input, which block this is and what its verdict is. The segment's
boundaries and length show up there too.

Drag-select any stretch of any track — statistics for the period appear below: time at
the PC, time away from the PC broken down into “stepped away” and “PC was off”,
percentages and the ratio. While dragging, a tooltip shows the boundaries and length of
the selection being made, and the stretch itself is highlighted on the lane. Clicking a
row in the block table gives the same statistics for that block.

Ctrl+wheel (⌘+wheel, or a pinch on the trackpad) zooms the lane in toward the point
under the cursor. The plain wheel, Shift+wheel and a two-finger horizontal swipe pan
the visible window; when there's nowhere left to pan — the whole day is shown, or the
window has hit midnight — the wheel scrolls the page instead. The “−”, “+” and “Whole
day” buttons do the same thing without a mouse. Zoom is a magnifying glass: it only
changes the lane, the axis and the histogram. The KPIs, the block table and “Games
played” are always computed for the whole day; numbers for an arbitrary stretch of the
day come from a selection. The lane won't zoom in closer than ten minutes — the buckets
are ten seconds each, and there's nothing further to show.

What the thresholds, KPIs, tracks, legend colours and table columns mean is written in
the tooltips of the corresponding elements.

The sliders at the top change the regime thresholds and recompute the verdict **across
the whole history at once**. The verdict is computed in the browser, so rebuilding the
report isn't needed for that. The “Reset thresholds” button returns all five to their
defaults.

The ← and → arrow keys page through days, and in “Week” view, through weeks. Clicking a
heatmap row or a day's bar opens that day. The “Theme” button switches between light
and dark, and by default the report follows the system setting. The language picker next
to it in the header switches the report's language the same way: the choice is remembered
in that browser, and by default the report opens in whichever language Windows is set to.

## What counts as a break

A block is cut by the absence of input, not by a window switch. Pausing a game without
leaving it and walking away is a break: the game window stays in the foreground, but
there's no input.

Pauses fall into three classes:

| Duration | What happens |
|---|---|
| shorter than `micro_gap` (3 min) | absorbed into the block |
| between `micro_gap` and `break_minutes` | the block continues: too short to be rest |
| `break_minutes` (15 min) or more | the block closes, the break counts |

## When recording was interrupted

An absence of data, by itself, means “the PC wasn't running” — the same thing as a
quiet evening without the computer. So a collector failure would look exactly like a
perfectly kept regime.

To keep that from happening, the program records the moments it starts, stops and
fails separately from the presence data. If recording was interrupted on the selected
day, the report shows a red banner at the top with the time and the reason, and the
tray tooltip changes and a balloon notification pops up. A gap in the data under the
banner may not be rest.

Failure details are additionally written to `error.log` next to the database.

## Limitations

What the program does approximately, or differently from what you might expect, is
collected in [docs/known-limitations.md](docs/known-limitations.md) — along with the reasons.

## Development

```
dotnet test tests/PcLogger.Core.Tests    # 121 tests
npm test                                 # 69 tests
dotnet run --project tools/DemoReport    # a report built from synthetic data
```

`PcLogger.Core` has no Windows dependency at all and builds anywhere. `PcLogger.App`
targets `net8.0-windows`, but thanks to `EnableWindowsTargeting` it publishes from
macOS too:

```
dotnet publish src/PcLogger.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## What is stored

`%LOCALAPPDATA%\PcLogger\pclogger.db` — SQLite. Ten-second buckets: the start time, how
many of the ten seconds had input, how many had gamepad activity, which app was in
focus. Plus a dictionary of paths and the lifetime intervals of processes with windows.

The report shows the last 90 days.
