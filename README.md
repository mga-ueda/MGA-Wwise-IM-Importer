# MGA Wwise IMImporter

[日本語 README](README.ja.md)

Windows tool (in development) that reads Nuendo/Cubase tracklist XML and Wave files, previews the waveform, writes split WAVs, and imports a Wwise Interactive Music hierarchy over WAAPI. Preview what you decide in the DAW, then deliver it to Wwise—typically much faster than hand-building the same structure in Authoring.

Designed so **the Interactive Music data in Wwise is the master waveform**: build one-shot and loop structure nondestructively on one master instead of pre-separating files.

With Nuendo/Cubase XML you get the most from tempo and bar data, and markers overlay the wave like painted regions. Marker-bearing WAVs from Logic and similar DAWs also work well; in-app marker add / set-move often removes the need for a separate waveform editor.

**Volume, fades, and markers are fully nondestructive.** Nothing is baked into source or split WAVs; EXPORT writes Wwise properties (MusicClip Fade, MusicFade, Make-Up Gain, Cue, and so on).

## Manual

- **[Japanese](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html)** / **[English](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.en.html)**
- Hub: [https://mga-ueda.github.io/MGA-Wwise-IMImporter/](https://mga-ueda.github.io/MGA-Wwise-IMImporter/)
- In the app, use the **Manual (`?`)** button left of the gear on the project bar (follows JP/EN)

## Getting started

1. Enable WAAPI (HTTP) in Wwise Authoring and select a destination object
2. Set this app’s export path under the connected project’s `Originals`
3. Load with one of these patterns, then run **EXPORT**
   - **Wave-only** — drop one `.wav` with no matching XML
   - **Multi-wave** — drop two or more `.wav` files with no matching XML
   - **WAV + matching XML** — drop a Nuendo/Cubase tracklist pair

See the manual section “Loading patterns” for details.

Self-contained Windows x64 packages are on [Releases](https://github.com/mga-ueda/MGA-Wwise-IMImporter/releases). Development targets .NET 8 (`net8.0-windows`).

## Settings files

Project settings, extra markers, Keep Last Session data, and so on live next to the exe in `MgaWwiseIMImporter.ini` and `MgaWwiseIMImporter.lastwave.<project>.json`. When updating, replace only the exe or back up those files first.

## Trademarks & licenses

- **Wwise®** / **Audiokinetic®** are trademarks of Audiokinetic Inc. This tool is unofficial and does not bundle Audiokinetic software. A valid Wwise license is required for WAAPI.
- **Nuendo®** / **Cubase®** / **Steinberg®** are trademarks of Steinberg Media Technologies GmbH. This tool is unofficial and only reads tracklist XML.
- Bundled: [NAudio](https://github.com/naudio/NAudio) (Ms-PL), [UDEV Gothic](https://github.com/yuru7/udev-gothic) (SIL OFL 1.1; `Licenses/LICENSE-UDEV-GOTHIC.txt`)

Footer text in the app:

> © 2026 MIYABI GAME AUDIO INC.  GitHub  
> Wwise® and Audiokinetic® are trademarks of Audiokinetic Inc.

## Version

The csproj `<Version>` (SemVer) is shared for display, update checks, and GitHub tags (e.g. `1.0.7-beta` ↔ tag `v1.0.7-beta`).
