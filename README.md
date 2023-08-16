# ARENA RenderFusion

RenderFusion with [ARENA](https://arenaxr.org/) and Unity!

<img alt="" src="Documentation~/images/demo.png">

## Requirements

We implement a custom layer on top of Unity's [WebRTC package](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/index.html). See their [Requirements](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/requirements.html).

## Repository Structure

```
<root>
├── io.conix.arena.renderfusion       // Main ARENA RenderFusion Package (must be included in all render pipelines!)
│   └── Samples~/SRP                  // Sample project for SRP workflows
├── io.conix.arena.renderfusion-urp   // Additional Package for URP workflows (must be included when using URP!)
│   └── Samples~/URP                  // Sample project for URP workflows
└── io.conix.arena.renderfusion-hdrp  // Additional Package for HDRP workflows (must be included when using HDRP!)
    └── Samples~/HDRP                 // Sample project for HDRP workflows
```

## Usage

See [Installation](Documentation~/install.md) section.

Once you have the relevant packages installed, press Play on the Unity Editor and enter the ARENA scene (`<Host Address>/<Namespace Name>/<Scene Name>`)!

## Samples

### Standard Render Pipeline
Open `io.conix.arena.renderfusion/Samples~/SRP`.

### Universal Render Pipeline
Open `io.conix.arena.renderfusion-urp/Samples~/URP`.

### High Definition Render Pipeline
Open `io.conix.arena.renderfusion-hdrp/Samples~/HDRP`.
