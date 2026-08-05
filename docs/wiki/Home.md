# Welcome to the Promptino Wiki

**Promptino** is a lightweight, clean, and highly customizable overlay teleprompter designed for Windows. It floats above active application windows with adjustable transparency and reading margins, allowing content creators, speakers, and presenters to read scripts seamlessly while maintaining eye contact with their camera.

---

## 🚀 Quick Navigation

- 📥 **[Installation & Quick Start](Installation-and-Quick-Start)**  
  Download pre-built releases, set up prerequisites, and launch your first teleprompter session.

- 📖 **[User Guide](User-Guide)**  
  Learn how to use script navigation markers (`[[marker:Label]]`), stage directions, speaker highlighting, mouse click-through mode, screen-share privacy, and global keyboard hotkeys.

- 🛠️ **[Development & Architecture](Development-and-Architecture)**  
  Explore the 4-layer architecture (`Promptino.App`, `Promptino.Core`, `Promptino.Platform`, `Promptino.Storage`), build instructions, test execution, and packaging scripts.

---

## ✨ Key Features at a Glance

* **Floating Prompter Overlay**: Stays on top of active windows with adjustable opacity, text size, line height, reading guides, and margin controls.
* **Script Marker Navigation**: Seamlessly jump between script sections using `[[marker:Label]]` tags or SRT/VTT subtitle timestamp markers.
* **Stage Directions & Speaker Badges**: Automatic badge highlighting for cues like `(pause 3s)` and speaker labels like `[HOST]:`.
* **Mouse Click-Through Mode**: Toggle interactive transparency (`WS_EX_TRANSPARENT`) to click directly through the prompter window to underlying apps.
* **Screen-Share Safe Mode**: Hide the prompter window from Zoom, Teams, or OBS screen captures using Win32 display affinity (`SetWindowDisplayAffinity`).
* **Remote Controller**: Floating mini window for play/pause, speed adjustments, and marker jumping without obscuring your view.
* **Global Keyboard Shortcuts**: Control playback globally even when Promptino is not the focused window.
* **Preset Profiles & Themes**: Custom color themes (Dracula, Nord, Solarized, Monokai, High Contrast) and saved profile configurations.
* **Bilingual Support**: Native English and Italian UI with dynamic runtime language switching.
* **100% Privacy & Local Storage**: No telemetry, no network tracking—all settings and scripts remain safely on your computer.

---

For issues, bug reports, or feature requests, visit the [GitHub Repository](https://github.com/lorenzoperrone/promptino).
