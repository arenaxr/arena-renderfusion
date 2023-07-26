# ARENA Hybrid Rendering for Unity

Enable Hybrid Rendering on [ARENA](https://arenaxr.org/) using Unity for remote rendering!

<img alt="" src="Documentation~/images/demo.png">

## Requirements

We implement a custom layer on top of Unity's [WebRTC package](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/index.html). See their [Requirements](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/requirements.html).

## Repository Structure

```
<root>
├── io.conix.arena.unity-hybrid         // Main ARENA Hybrid Rendering Package (must be included in all render pipelines!)
│   └── Samples~/SRP                    // Sample project for SRP workflows
├── io.conix.arena.unity-hybrid-urp     // Additional Package for URP workflows (must be included when using URP!)
│   └── Samples~/URP                    // Sample project for URP workflows
└── io.conix.arena.unity-hybrid-hdrp    // Additional Package for HDRP workflows (must be included when using HDRP!)
    └── Samples~/HDRP                   // Sample project for HDRP workflows
```

## Usage

See [Installation](Documentation~/install.md) section.

## Samples

### Standard Render Pipeline
Open `io.conix.arena.unity-hybrid/Samples~/SRP`.

### Universal Render Pipeline
Open `io.conix.arena.unity-hybrid-urp/Samples~/URP`.

### High Definition Render Pipeline
Open `io.conix.arena.unity-hybrid-hdrp/Samples~/HDRP`.
