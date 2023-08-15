# Installation

First, clone the repository
```
git clone git@github.com:arenaxr/arena-renderfusion-unity.git
```

To install the package(s), select `Window/Package Manager` from the menu bar.

![Install Package Manager from menu bar](images/package_manager.png)

Then, select `+` and `Add package from disk`.

![Install Package from disk](images/install_from_disk.png)

Then find
```
<root>/io.conix.arena.renderfusion/package.json
```
from the cloned repo.

## URP

If you are working in URP, __also__ install
```
<root>/io.conix.arena.renderfusion-urp/package.json
```
IMPORTANT: Your URP Settings should include the `RGB Depth Feature` post processing effect that comes with this library.

![URP settings](images/urp_settings.png)

## HDRP

If you are working in HDRP, __also__ install
```
<root>/io.conix.arena.renderfusion-hdrp/package.json
```
IMPORTANT: Your HDRP Global Settings should include the `ArenaUnity.RenderFusion.RGBDepthShaderHD` in the `After Post Process` item under the `Custom Post Process Order` list.

![URP settings](images/hdrp_settings.png)

# Prefabs

You can include Prefabs from `io.conix.arena.renderfusion/Runtime/Prefabs`
- Include `io.conix.arena.renderfusion-urp/Runtime/Prefabs` if you are using URP.
- Include `io.conix.arena.renderfusion-hdrp/Runtime/Prefabs` if you are using HDRP.
