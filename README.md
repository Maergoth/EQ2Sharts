# EQ2Sharts — ACT Queue Plugin for EverQuest 2

EQ2Sharts is a single-file ACT (Advanced Combat Tracker) plugin that manages a player queue via in-game chat. It was designed for distributing Hearts (or any item/buff) in EQ2 raids, letting players request a spot in line and operators dequeue them as they're served.

Created by Sichi, injected with nonsense by Maergoth and Kaizar.

https://discord.gg/reverb

## Installation

1. [Right-Click and "Save Link As" to Download EQ2Sharts.cs](https://raw.githubusercontent.com/Maergoth/EQ2Sharts/main/EQ2Sharts.cs)
2. Go to the "plugins" tab on ACT and select the file
3. Import it, make sure the checkbox is active
4. Customize the "channels" you're watching, and set the "Enqueue" and "Dequeue" text to match
5. Activity Log can provide some hints if it's not working

<img width="1024" height="770" alt="image" src="https://github.com/user-attachments/assets/48de3d3f-650e-4bd1-9887-8c2c7a19580d" />



## Features

- **Chat-driven queue** — Players enqueue themselves by typing a trigger phrase in a monitored channel. An operator dequeues them by name when served.
- **Overlay window** — A topmost, resizable overlay displays the numbered queue over the game. Supports click-through mode, adjustable opacity, font scaling, and custom background/foreground colors.
- **Multiple channels** — Monitor several chat channels simultaneously (raid, group, custom channels) by listing one per line.
- **Flexible chat format support** — Handles both EQ2 `tells` and `says to the` syntaxes, with or without channel numbers.
- **Wait time display** — Optional per-player wait timer shown on the overlay, updating live every second.
- **Average wait time** — Logged to the activity log on each dequeue, tracking a running average across the session.
- **Invert order** — Checkbox to flip the overlay display from top-to-bottom to bottom-to-top.
- **Priority bump** — When enabled, duplicate enqueue requests bump the player up one slot instead of being ignored.
- **Bump notification** — Text-to-speech announces "bump" when a player is bumped (requires Priority Bump).
- **Auto-hide when empty** — The overlay automatically hides when the queue is empty and reappears when someone joins.
- **Persistent settings** — All configuration (channels, patterns, overlay position/size, colors, checkboxes) is saved to XML and restored on reload.

## How It Works

### Log Line Parsing

ACT fires `OnLogLineRead` for every line in the EQ2 log file. The plugin applies a pre-filter (checks for a `"` character) and then matches against a compiled regex built from the configured channel names.

EQ2 log lines use two formats for chat:

```
\aPC -1 PlayerName:PlayerName\/a tells ChannelName (N), "message"
\aPC -1 PlayerName:PlayerName\/a says to the raid party, "message"
You tell ChannelName (N), "message"
You say to the raid party, "message"
```

The channel regex is constructed at runtime as:

```
(?:\aPC -?\d+ [^:]+:(\w+)\\/a|(You)) (?:tells? (?:Chan1|Chan2)(?: \(\d+\))?,|says? to the (?:Chan1|Chan2)[^,]*,) "(.+?)"
```

- **Group 1** captures the player name from the `\aPC` link format (the portion after the colon).
- **Group 2** captures `You` when the speaker is the local player.
- **Group 3** captures the message content inside quotes.

### Enqueue / Dequeue

Once a sender and message are extracted:

1. The message is tested against each **enqueue pattern** (user-configurable regexes). On match, the sender is added to the tail of a `LinkedList<string>` and tracked in a `Dictionary` for O(1) duplicate detection.
2. If no enqueue pattern matches, the message is tested against each **dequeue pattern**. These regexes must contain a capture group for the target player name (e.g., `^HEARTS TO (\w+)$`). The matched name is removed from the queue.

A timestamp (`DateTime.Now`) is recorded at enqueue time. On dequeue, the elapsed time is logged along with a running average across all dequeues in the session.

### Overlay

`QueueOverlayForm` is a `TopMost`, `SizableToolWindow` form containing a single `Label`. The queue is rendered as numbered lines (`1. Name`, `2. Name`, ...) joined with newlines.

- **Click-through** mode removes the title bar and sets `WS_EX_TRANSPARENT | WS_EX_LAYERED` via `SetWindowLong`, making the window pass mouse events to whatever is underneath.
- **Invert** reverses the list before display.
- **Wait time** appends `(Xm Ys)` to each entry. A 1-second `Timer` triggers display refreshes while enabled.
- **Auto-hide** hides or shows the overlay based on queue content when enabled, respecting the Show Overlay checkbox.

### Settings Persistence

The plugin uses ACT's `SettingsSerializer` to bind UI controls to an XML config file (`%APPDATA%\Advanced Combat Tracker\Config\EQ2Sharts.config.xml`). All text boxes, checkboxes, trackbars, and the overlay bounds are serialized on plugin exit and restored on load.

## Configuration

| Setting | Description |
|---|---|
| **Channel Names** | One channel per line. Matches both `tells <channel>` and `says to the <channel>`. |
| **Enqueue Patterns** | Regex patterns (one per line) matched against message content. No capture group needed — the sender is enqueued. |
| **Dequeue Patterns** | Regex patterns (one per line) with a capture group `()` for the player name to remove from the queue. |
| **Show Overlay** | Toggle overlay visibility. |
| **Click-Through** | Makes the overlay non-interactive (no title bar, passes clicks through). |
| **Invert** | Reverses display order (bottom to top). |
| **Show Wait Time** | Displays elapsed time next to each queued name. |
| **Priority Bump** | Duplicate enqueue requests bump the player up one slot. |
| **Bump Notification** | Text-to-speech says "bump" when a player is bumped (requires Priority Bump). |
| **Auto-Hide When Empty** | Automatically hides the overlay when the queue is empty and shows it when someone joins. |
| **Opacity** | Overlay window transparency (10–100%). |
| **Font Scale** | Overlay text size multiplier (50–300%). |
| **Background / Text** | Color pickers for overlay appearance. |
