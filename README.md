# ARENA Hybrid Rendering for Unity

Enable Hybrid Rendering on [ARENA](https://arenaxr.org/) using Unity for remote rendering!

<img alt="" src="images/demo.png">

# Usage
```
git clone git@github.com:arenaxr/arena-hybrid-rendering.git
```

To install the package(s), click Window->Package Manager->`+`->Add package from disk.

Then select `io.conix.arena.unity-hybrid/package.json` from this repo.<br>
- If you are working in URP, also install `io.conix.arena.unity-hybrid-urp/package.json`. Your URP Settings should include the `RGB Depth Feature` post processing effect that comes with this library.<br>
- If you are working in HDRP, also install `io.conix.arena.unity-hybrid-hdrp/package.json`. Your HDRP Global Settings should include the `ArenaUnity.HybridRendering.RGBDepthShaderHD` in the `After Post Process` item under the `Custom Post Process Order` list.

You can include Prefabs in `io.conix.arena.unity-hybrid/Runtime/Prefabs`
- Include `io.conix.arena.unity-hybrid-urp/Runtime/Prefabs` if you are using URP.
- Include `io.conix.arena.unity-hybrid-hdrp/Runtime/Prefabs` if you are using HDRP.

# Samples

## Standard Render Pipeline
Open `io.conix.arena.unity-hybrid/Samples~/SRP`.

## Universal Render Pipeline
Open `io.conix.arena.unity-hybrid-urp/Samples~/URP`.

## High Definition Render Pipeline
Open `io.conix.arena.unity-hybrid-hdrp/Samples~/HDRP`.

# Development

Development uses Unity Editor Version 2022.3+
