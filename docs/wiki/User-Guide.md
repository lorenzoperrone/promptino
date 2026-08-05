# Promptino User Guide

This user guide provides detailed instructions on how to use Promptino's advanced features, script markup syntaxes, privacy settings, and controller windows.

---

## 📌 Script Navigation Markers

Markers allow you to jump instantly to specific sections in your script without manual scrolling.

### 1. Embedded Marker Syntax (`[[marker:Label]]`)
Insert marker tags anywhere in your script text:
```markdown
Welcome everyone to today's presentation.

[[marker:Introduction]]
Today we will discuss key product milestones...

[[marker:Demo Segment]]
Now let's switch to the live demonstration...
```
* Markers are automatically stripped from the prompter display text so they are not read aloud.
* They appear in the **Script Markers List** on the Control Panel and Remote Controller.
* Clicking a marker jumps progress directly to its word position.

### 2. Subtitle File Timestamps (`.srt` / `.vtt`)
When loading `.srt` or `.vtt` subtitle files, Promptino automatically converts subtitle timecodes (e.g. `00:01:30`) into navigation markers.

---

## 🎬 Stage Directions & Speaker Badges

Promptino parses script lines for parenthetical cues and speaker prefixes to apply visual badge styling:

* **Speaker Prefixes**: Lines starting with `[SPEAKER]:` or `ALICE:` are highlighted with a distinct speaker badge.
* **Stage Directions**: Parenthetical cues like `(pause 3s)`, `[applause]`, or `(smile at camera)` are styled with muted/colored background pills so you can distinguish non-spoken cues at a glance.

---

## 🖱️ Mouse Click-Through Mode

When delivering a presentation or hosting a video call on Zoom, Teams, or Google Meet, you might need to click buttons on windows directly underneath the teleprompter.

* **How to Enable**: Click **Toggle Click-Through Mode** (`Ctrl+Shift+T`).
* **Behavior**: Uses Win32 `WS_EX_TRANSPARENT` style. Mouse clicks pass straight through the prompter window to underlying windows.
* **Disabling**: Press the hotkey shortcut again or use the Remote Controller to restore standard mouse interactivity.

---

## 🛡️ Screen-Share Safe Mode (Privacy Protection)

Screen-Share Safe Mode prevents meeting participants from seeing your teleprompter script when you share your screen on Zoom, Microsoft Teams, Discord, or OBS.

* **How to Enable**: Toggle **Screen-Share Safe Mode** in the Control Panel settings.
* **Under the Hood**: Uses Windows `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` Win32 API.
* **Result**: The prompter remains completely visible to you on your monitor, but appears totally invisible (or transparent) to screen recorders and meeting captures.

---

## 🎮 Remote Mini Controller

The **Remote Mini Controller** is a compact floating window designed to sit next to your webcam or application window:

* **Controls**: Play, Pause, Reset, Increase/Decrease WPM, Previous/Next Marker.
* **Progress Indicator**: Real-time progress bar showing reading progress ratio.
* **Top-Most**: Automatically stays on top without obscuring your script view.

---

## ⌨️ Global Keyboard Hotkeys

Control Promptino even when working in another application. Default shortcuts:

| Action | Default Global Shortcut |
| :--- | :--- |
| **Toggle Play / Pause** | `Ctrl + Alt + Space` |
| **Reset Script Position** | `Ctrl + Alt + R` |
| **Increase Reading Speed (+10 WPM)** | `Ctrl + Alt + Up` |
| **Decrease Reading Speed (-10 WPM)** | `Ctrl + Alt + Down` |
| **Jump to Next Marker** | `Ctrl + Alt + PageDown` |
| **Jump to Previous Marker** | `Ctrl + Alt + PageUp` |

*Hotkeys can be customized or rebinded in Settings (`Ctrl+,`). Conflict warnings are surfaced if another app occupies a gesture.*

---

## 🎨 Theme Presets & Profiles

Promptino includes curated color palettes optimized for eye comfort and readability:
* **Default Light / Dark**
* **Dracula** (`#F8F8F2` on `#282A36`)
* **Nord** (`#D8DEE9` on `#2E3440`)
* **Gruvbox Dark** (`#EBDBB2` on `#282828`)
* **Solarized Dark** (`#839496` on `#002B36`)
* **Monokai** (`#F8F8F2` on `#272822`)
* **Matrix Green** (`#00FF00` on `#000000`)
* **Colorblind Safe** (`#F0E442` on `#0072B2`)

*Saved settings can be exported or saved into custom User Profiles.*
