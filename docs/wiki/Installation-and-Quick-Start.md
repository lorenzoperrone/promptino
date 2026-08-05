# Installation & Quick Start Guide

This guide covers system requirements, downloading Promptino, running pre-built releases, and basic configuration for your first teleprompter session.

---

## 🖥️ System Requirements

* **Operating System**: Windows 10 / Windows 11 (64-bit `win-x64`).
* **Runtime**: .NET 10 Desktop Runtime (included in self-contained release packages).
* **Display**: Supports single or multi-monitor setups.

---

## 📥 Downloading Promptino

You can obtain Promptino in two ways:

1. **Self-Contained Executable Package (Recommended)**:
   * Download the latest `Promptino-vX.Y.Z-win-x64.zip` from the [GitHub Releases](https://github.com/lorenzoperrone/promptino/releases) page.
   * Extract the ZIP archive to a folder of your choice (e.g. `C:\Tools\Promptino`).
   * Launch `Promptino.App.exe`.

2. **Classic Windows Installer (`.msi` / Setup Executable)**:
   * Download `PromptinoSetup-x64.exe` or `.msi` from Releases.
   * Run the installer wizard to place Promptino in your Start Menu and Program Files.

---

## 🚀 Quick Start (5 Steps)

1. **Launch Promptino**: Open `Promptino.App.exe`. The **Main Control Panel** window will appear.
2. **Load a Script**: Click **Open Script File** (`Ctrl+O`) and select a text (`.txt`), Markdown (`.md`), or Subtitle (`.srt`, `.vtt`) file.
3. **Calibrate Reading Speed**: Set your target reading speed in **Words Per Minute (WPM)** using the WPM slider or numerical input (Default: 130 WPM).
4. **Customize Prompter Display**:
   * Open the **Prompter Window** by clicking **Show Prompter Window**.
   * Adjust font size, text alignment, background opacity, and color theme (e.g. Nord, Dracula).
5. **Start Reading**: Press **Play** (or press the global hotkey `Ctrl+Alt+Space`) to start automatic scrolling.

---

## ⚙️ Initial Settings & File Storage

Promptino stores configuration files locally in your User AppData directory:
* **Settings**: `%APPDATA%\Promptino\settings.json`
* **Recent Files**: `%APPDATA%\Promptino\recent-files.json`
* **Application Logs**: `%APPDATA%\Promptino\logs\promptino.log`

> [!NOTE]
> Promptino does not require administrative privileges to run or save settings.
