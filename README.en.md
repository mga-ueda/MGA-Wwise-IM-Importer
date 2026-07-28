# MGA Wwise IMImporter

[日本語](README.md)

Take Interactive Music from “hand-built Authoring” to “listen, decide, and deliver.”

## What makes this app stand out

**Carry the music structure you designed in the DAW straight into Wwise Interactive Music.**

Building Music Playlist Containers, Music Switch Containers, Music Segments, Music Tracks, State Groups, transitions, cues, and fades one by one in Authoring takes time. Preview on the waveform, decide transitions and layers by ear, then push a consistent Wwise structure with a single EXPORT. **Far faster than hand-building the same graph.**

**You do not need Nuendo / Cubase.** A WAV with markers from Logic or another DAW that can place markers already saves a lot of work. This app can also add markers and move loop-marker pairs itself—so **a dedicated waveform editor is often unnecessary**, which is another major benefit.

**Those DAWs make it especially powerful.** Export a marker-track XML and you get the most out of tempo, bar, and time-signature data. Markers and cycles can be placed on the waveform as if painting them on, so Entry / Exit / loop design stays visual.

The workflow assumes **the Interactive Music data in Wwise is the master**. Keep one-shot and loop material on a single master waveform instead of pre-separating files; build structure nondestructively, then EXPORT cut ranges plus Wwise properties (MusicClip Fade / MusicFade / Make-Up Gain / Cue, and so on). Gains and fades are not baked into the source WAV.

- Music Playlist transition preview (Exit Source At / Fade In / Fade Out / Play -E)
- Vertical layers via grouping (Alt layering / Additive Layer, Group Fade, Change Occurs At)
- Streaming (Prefetch Length / Look-ahead Time) and Keep Layer Balance (Make-Up Gain)
- Automatic Music hierarchy creation over WAAPI
- **Drop multiple waves at once, or use marker-track XML, to implement any number of pieces together**

## Manual & download

- Manual: [Japanese](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html) · [English](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.en.html) · [Hub](https://mga-ueda.github.io/MGA-Wwise-IMImporter/)
- New here? Start with the [quick start](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.en.html#quickstart) (a single WAV is enough) and [preparing your material](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.en.html#prepare) (XML and marker rules)
- In-app: project-bar **Manual (`?`)** (left of the gear; follows JP/EN)
- Builds: [Releases](https://github.com/mga-ueda/MGA-Wwise-IMImporter/releases)

Wwise® / Audiokinetic® and Nuendo® / Cubase® / Steinberg® are trademarks of their respective owners. This tool is unofficial.
