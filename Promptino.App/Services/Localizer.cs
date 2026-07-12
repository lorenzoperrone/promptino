using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace Promptino.App.Services;

public static class Localizer
{
    private static readonly Dictionary<string, string> FallbackMap = new()
    {
        ["AppName"] = "Promptino",
        ["AppTagline"] = "Your tiny teleprompter overlay",
        ["AppDescription"] = "Local script teleprompter with fast setup, readable playback, and local-only persistence.",
        ["ControlPanelTitle"] = "Promptino Control Panel",
        ["PrompterTitle"] = "Promptino Prompter",
        ["RemoteTitle"] = "Promptino Remote",
        ["BtnLoadScript"] = "Load Script",
        ["BtnEditScript"] = "Edit Script",
        ["BtnShowPrompter"] = "Show Prompter",
        ["BtnHidePrompter"] = "Hide Prompter",
        ["BtnShowRemote"] = "Show Remote",
        ["BtnHideRemote"] = "Hide Remote",
        ["BtnApply"] = "Apply",
        ["BtnBrowse"] = "Browse...",
        ["BtnRecord"] = "Record",
        ["BtnRecording"] = "Press keys...",
        ["CardPlayback"] = "Playback",
        ["BtnPlay"] = "Play",
        ["BtnPause"] = "Pause",
        ["BtnReset"] = "Reset",
        ["BtnPrevMarker"] = "Prev Marker",
        ["BtnNextMarker"] = "Next Marker",
        ["TooltipPlay"] = "Start scrolling (Space or Global Hotkey)",
        ["TooltipPause"] = "Pause scrolling (Space or Global Hotkey)",
        ["TooltipReset"] = "Reset playback to start (R)",
        ["TooltipPrevMarker"] = "Go to previous marker (PageUp or Global Hotkey)",
        ["TooltipNextMarker"] = "Go to next marker (PageDown or Global Hotkey)",
        ["LabelSpeed"] = "Speed: {0} WPM",
        ["LabelSmoothScrolling"] = "Smooth Scrolling Mode",
        ["DriverStatus"] = "Active driver: {0}",
        ["DriverInfo"] = "Render-aligned follows compositor commits. Oversampled timer is an experimental higher-frequency UI timer.",
        ["ChkHighPerformance"] = "High-performance text scrolling",
        ["DriverIdle"] = "idle",
        ["DriverCompositor"] = "compositor",
        ["DriverOversampled"] = "oversampled timer",
        ["DriverFallback"] = "timer (fallback)",
        ["CardMarkers"] = "Script Markers",
        ["PlaceholderMarkerName"] = "Marker Name",
        ["BtnAddMarker"] = "Add Marker",
        ["BtnRenameMarker"] = "Rename",
        ["BtnDeleteMarker"] = "Delete",
        ["BtnJumpToMarker"] = "Jump To",
        ["MarkersInfo"] = "Markers added here are session-only and will not modify the source file.",
        ["CardReadingSurface"] = "Reading Surface",
        ["LabelAppTheme"] = "App Theme:",
        ["ThemeLight"] = "Light",
        ["ThemeDark"] = "Dark",
        ["LabelColorTheme"] = "Color Theme:",
        ["PresetDefault"] = "Default (Promptino Dark)",
        ["PresetDracula"] = "Dracula",
        ["PresetNord"] = "Nord",
        ["PresetGruvbox"] = "Gruvbox Dark",
        ["PresetSolarized"] = "Solarized Dark",
        ["PresetMonokai"] = "Monokai",
        ["PresetMatrix"] = "Matrix Green",
        ["PresetColorblind"] = "Colorblind Safe (Blue/Yellow)",
        ["LabelReadingGuide"] = "Reading Guide:",
        ["GuideNone"] = "None",
        ["GuideLine"] = "Line Only",
        ["GuideBand"] = "Highlight Band Only",
        ["GuideBoth"] = "Both (Line + Band)",
        ["LabelTextAlignment"] = "Text Alignment:",
        ["AlignLeft"] = "Left",
        ["AlignCenter"] = "Center",
        ["AlignRight"] = "Right",
        ["AlignJustified"] = "Justified",
        ["ChkAlwaysOnTop"] = "Keep Prompter On Top",
        ["ChkScreenShareSafe"] = "Screen-Share Safe Mode (hide from capture)",
        ["ChkMirrorText"] = "Mirror Text Horizontally",
        ["LabelTextSize"] = "Text Size: {0}",
        ["LabelWindowOpacity"] = "Prompter Opacity: {0}%",
        ["LabelReadingMargin"] = "Reading Margin: {0}px",
        ["CardCleanup"] = "Script Cleanup Preview",
        ["CleanupInfo"] = "Preview cleanup before sending the result to the prompter. Source files are never modified.",
        ["ChkCleanupTimestamps"] = "Remove timestamps and subtitle cue numbers",
        ["ChkCleanupMetadata"] = "Remove metadata rows and front matter",
        ["ChkCleanupTables"] = "Flatten markdown tables into readable text",
        ["BtnPreviewCleanup"] = "Refresh Preview",
        ["BtnApplyCleanup"] = "Apply Cleanup To Prompter",
        ["BtnRestoreOriginal"] = "Restore Loaded Script",
        ["CleanupStateLoaded"] = "Cleanup: loaded script active",
        ["CleanupStateCleaned"] = "Cleanup: cleaned script active in prompter",
        ["CleanupStatePreviewReady"] = "Cleanup: preview ready, loaded script still active",
        ["LabelOriginalSource"] = "Original source",
        ["LabelCleanedPreview"] = "Cleaned preview",
        ["CardWarnings"] = "Warnings and Recovery",
        ["CardHotkeys"] = "Global Hotkeys",
        ["LabelPlayPauseHotkey"] = "Play/Pause Hotkey:",
        ["LabelNextMarkerHotkey"] = "Next Marker Hotkey:",
        ["LabelPrevMarkerHotkey"] = "Prev Marker Hotkey:",
        ["BtnApplyHotkeys"] = "Apply Hotkeys",
        ["CardExternalEditor"] = "External Editor",
        ["ExternalEditorInfo"] = "Configure the executable path of your favorite text editor (e.g., VS Code, Notepad++). Leave blank for default (Notepad).",
        ["CardProfiles"] = "Profiles",
        ["PlaceholderProfileName"] = "Profile name",
        ["BtnSaveProfile"] = "Save Profile",
        ["BtnLoadProfile"] = "Load Profile",
        ["BtnDeleteProfile"] = "Delete Profile",
        ["RemoteSpeedLabel"] = "SPEED",
        ["RemoteWpmLabel"] = "WPM",
        ["RemoteEditButton"] = "EDIT",
        ["RemoteStatusPlaying"] = "Playing",
        ["RemoteStatusPaused"] = "Paused",
        ["RemoteStatusReady"] = "Ready",
        ["BtnRemotePlay"] = "▶ Play",
        ["BtnRemotePause"] = "⏸ Pause",
        ["BtnRemoteReset"] = "⏹ Reset",
        ["BtnRemotePrev"] = "◀ Marker",
        ["BtnRemoteNext"] = "Marker ▶",
        ["MsgNoScriptLoaded"] = "No script loaded.",
        ["MsgDefaultSpeedSaved"] = "Default speed saved: {0} WPM",
        ["MsgCalibrationSkipped"] = "Calibration skipped. You can start reading now.",
        ["MsgOpenedExternalEditor"] = "Opened in external editor: {0}",
        ["MsgFailedExternalEditor"] = "Failed to open in external editor.",
        ["MsgLoadedScript"] = "Loaded: {0}",
        ["MsgAutoReloaded"] = "Auto-reloaded: {0}",
        ["MsgSettingsSaveFailure"] = "Could not save preferences locally. Changes apply this session only.",
        ["MsgSettingsRecovery"] = "Local settings were unavailable, so safe defaults were loaded for this session.",
        ["WarnLoadScriptFirst"] = "Load a script first, then press Play.",
        ["WarnPressKeys"] = "Press a combination of keys (e.g. Ctrl+Alt+Space). Esc to cancel.",
        ["WarnUnsupportedKey"] = "Unsupported key for shortcuts. Try again or press Esc.",
        ["WarnModifierRequired"] = "Global shortcuts require at least one modifier key (Ctrl, Alt, Shift, Win). Try again.",
        ["WarnAlwaysOnTopApplyFailure"] = "Always-on-top preference saved, but this session could not apply it.",
        ["WarnLoadScriptFirstEdit"] = "Load a script first, then try editing again.",
        ["WarnProfilesReadRecovery"] = "Profiles could not be read and were reset for this session.",
        ["WarnProfilesSaveFailure"] = "Could not save profiles locally. Changes were not persisted.",
        ["WarnProfilesDeleteFailure"] = "Could not update profiles locally. Delete was not persisted.",
        ["WarnProfileNameRequired"] = "Profile name required.",
        ["LabelSmoothRenderAligned"] = "Render-aligned (Recommended)",
        ["LabelSmoothOversampled"] = "Oversampled timer (Experimental)",
        ["LabelAppLanguage"] = "Language:",
        ["LangAuto"] = "System Default (Auto)",
        ["LangEn"] = "English",
        ["LangIt"] = "Italian (Italiano)",
        ["DefaultMarkerName"] = "Marker {0}"
    };

    public static string Get(string key, params object[] args)
    {
        string? str = null;

        if (Application.Current != null && Application.Current.TryFindResource(key, out var val))
        {
            str = val as string;
        }

        if (str == null)
        {
            FallbackMap.TryGetValue(key, out str);
        }

        if (str != null)
        {
            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(str, args);
                }
                catch
                {
                    return str;
                }
            }
            return str;
        }

        return key;
    }
}
