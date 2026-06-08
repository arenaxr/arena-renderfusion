# ARENA RenderFusion

High-quality remote/split rendering on the web! Stream Unity scenes into the browser using RenderFusion with [ARENA](https://arenaxr.org/)!

RenderFusion performs "remote rendering", which offloads rendering to a nearby machine and streams the results as video to a web browser. RenderFusion also employs "split rendering", which renders some portions of the 3D scene locally on the browser to reduce latency.

Note: this is an *experimental* ARENA feature, but is deployed on the main branch, so it works out the box!

<img alt="demo image" src="Documentation~/images/demo.png">

(Scene from https://assetstore.unity.com/packages/essentials/tutorial-projects/book-of-the-dead-environment-hdrp-121175).

See demo videos: [RenderFusion](https://www.youtube.com/watch?v=6mA4k9myuOM) and [Volumetric Capture and Streaming using RenderFusion](https://www.youtube.com/watch?v=561-RQ1zVc4)!

## Requirements

We implement a custom layer on top of Unity's [WebRTC package](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/index.html). See their [Requirements](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/requirements.html).

## Usage

See [Installation](Documentation~/install.md) section. *For AR/VR clients, it is HIGHLY recommended you use this package in __URP__ for the best performance.*

Once you have the relevant packages installed, press Play on the Unity Editor or build the Unity application, then enter the ARENA scene (`https://arenaxr.org/<ARENA User Name>/<Scene Name>`) on a web browser!

## Authentication & Permissions

RenderFusion uses a dedicated MQTT topic namespace (`/r/`) that requires elevated permissions beyond normal scene access. **Setting `public_read` / `public_write` on a scene is not sufficient** — the `/r/` topic is only granted to authenticated scene owners and editors.

### Requirements

1. **Google authentication** — you must use `Auth: Google` (not Anonymous) on the `ArenaClientScene` component. Anonymous users cannot host RenderFusion.
2. **Scene ownership or editor role** — you must either:
   - Own the scene (your ARENA username matches the scene's namespace), **or**
   - Be added as an **editor** of the scene by its owner (via the ARENA scene editor or API)
3. **Render rights token** — the MQTT auth token must include `renderfusionid` permissions:
   - **In the Unity Editor**: this is handled automatically — the ARENA library detects the RenderFusion package and requests render permissions during authentication.
   - **In standalone builds**: the RenderFusion component automatically sets `requestRemoteRenderRights = true` before connecting.

### Why `public_read` / `public_write` is not enough

The scene's public read/write toggles control the standard object (`/o/`), user (`/u/`), presence (`/x/`), and chat (`/c/`) topics. The render (`/r/`) and environment (`/e/`) topics are privileged — they are only granted to authenticated owners/editors who explicitly request host permissions. This prevents unauthorized users from injecting render streams into scenes they don't control.

## Troubleshooting

### QoS 128 / "Subscribed FAILED"

If you see `Subscribed FAILED to: realm/s/<ns>/<scene>/r/+/+/-/#` in the Unity console, the MQTT broker rejected the subscription. This means your auth token does not include `/r/` permissions. Check:

- You are signed in with Google auth (not Anonymous)
- Your username matches the scene's namespace, or you are an editor of the scene
- The RenderFusion package is properly installed (the ARENA library auto-detects it in the editor)

### "Permissions FAILED Sending" for `/r/` topics

The warning `Scene remote rendering changes for topic type 'r' only available to Google authenticated scene Owner and Editors by API request` means your publish permissions do not cover the render topic. The same ownership/editor requirements apply as above.

### Shader errors (`_WorkflowMode` or pink/magenta materials)

If you see errors about missing shader properties like `_WorkflowMode`, or GLTF models render as pink/magenta, this is typically a **glTFast shader variant stripping issue**. Unity's build pipeline strips shader variants it considers unused, but glTFast needs them at runtime for glTF import.

To fix:
1. Go to **Edit > Project Settings > Graphics**
2. Under **Shader Preloading**, add the glTFast shaders (`glTF/PbrMetallicRoughness`, `glTF/PbrSpecularGlossiness`, `glTF/Unlit`) to the **Always Included Shaders** list
3. Alternatively, create a `ShaderVariantCollection` that includes the required variants

If the error specifically mentions `_WorkflowMode`, your URP version may not match what glTFast expects. Ensure your URP package is compatible with your glTFast version.

## Alternate Server Usage

In addition to the above, you may deploy your own ARENA webserver if you do not wish to use our test deployment server at [arenaxr.org](https://arenaxr.org). You can follow our setup guidelines in [arenaxr/arena-services-docker](https://github.com/arenaxr/arena-services-docker), to run your own server.

## Samples

### Universal Render Pipeline (recommended)
Open `Samples~/URP`.

### Standard Render Pipeline
Open `Samples~/SRP`.

### High Definition Render Pipeline
Open `Samples~/HDRP`.

## Quick Start

For a quick start, simply open `Samples~/URP` using the Unity Hub. Click on the `ARENARenderFusion` Game Object, change the Scene Name to your favorite ARENA scene, then press Play!

## Documentation
- [Contributing](CONTRIBUTING.md)

## License
See the [LICENSE](LICENSE) file.

If you find this project helpful for any research-related purposes, please consider citing our paper:
```
@inproceedings{lu2023renderfusion,
  author = {Lu, Edward and Bharadwaj, Sagar and Dasari, Mallesham and Smith, Connor and Seshan, Srinivasan and Rowe, Anthony},
  booktitle = {2023 International Symposium on Mixed and Augmented Reality (ISMAR)},
  title = {RenderFusion: Balancing Local and Remote Rendering for Interactive 3D Scenes},
  year = {2023}
}
```
