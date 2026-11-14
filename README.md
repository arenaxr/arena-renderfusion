# ARENA RenderFusion

High-quality remote/split rendering on the web! Stream Unity scenes into the browser using RenderFusion with [ARENA](https://arenaxr.org/)!

RenderFusion performs "remote rendering", which offloads rendering to a nearby machine and streams the results as video to a web browser. RenderFusion also employs "split rendering", which renders some portions of the 3D scene locally on the browser to reduce latency.

Note: this is an *experimental* ARENA feature, but is deployed on the main branch, so it works out the box!

<img alt="demo image" src="Documentation~/images/demo.png">

(Scene from https://assetstore.unity.com/packages/essentials/tutorial-projects/book-of-the-dead-environment-hdrp-121175).

See demo videos: [RenderFusion](https://www.youtube.com/watch?v=6mA4k9myuOM) and [Volumetric Capture and Streaming using RenderFusion](https://www.youtube.com/watch?v=561-RQ1zVc4)!

## Requirements

The samples target Unity 6.3 LTS, tested with Unity 6000.3.28f1 and URP/HDRP 17.3.0. Local package dependencies use relative paths within this checkout; no setup script or machine-specific package paths are required. The ARENA Unity dependency is pinned to a Git revision so each checkout resolves the same library code.

The core Unity package lives in `Packages/RenderFusion`; the repository root is a private development workspace. To install from Git, use `https://github.com/arenaxr/arena-renderfusion.git?path=/Packages/RenderFusion`. Keeping packages separate from the sample projects prevents Unity from assigning downloaded dependency scripts to an enclosing local package.

Existing installations must update their core and addon Git URLs to the paths in [Installation](Documentation~/install.md). Package identities and existing script/prefab GUIDs are preserved.

We implement a custom layer on top of Unity's [WebRTC package](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/index.html). See their [Requirements](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/requirements.html).

Depth is streamed as 8-bit grayscale using the existing ARENA browser format. Every render pipeline encodes eye-space distance in metres divided by `200`, independently of the Unity camera's clipping planes. This matches the browser's `/ 50` decoder with its default 10,000 m far plane. Geometry beyond approximately 200 m saturates; an empty background stays white.

Desktop cameras match ARENA's default 80-degree field of view and 0.005–10,000 m clipping interval. Stereo cameras preserve the supplied off-axis projection and use matching depth parameters; returning to desktop restores automatic projection. Stereo still assumes parallel eyes separated by the supplied IPD and matching browser/stream viewport aspects. Validate alignment on the target headset: the existing browser messages do not provide full per-eye poses or actual XR framebuffer dimensions.

Validated on macOS and Linux with Unity 6000.3.28f1: 149 EditMode checks per platform, plus live desktop Chrome alignment and reconnect checks. Standalone builds and headset alignment have not been validated end to end.

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

## Performance & Optimization

### Unity Settings (`ArenaClientScene` Component)

| Setting | Recommended | Why |
|---------|-------------|-----|
| **Load Persisted Objects** | ☐ Unchecked | The Unity RenderFusion app is the renderer — it doesn't need to download and process scene objects from MQTT. Reduces bandwidth and memory. |
| **Load Live Objects** | ☐ Unchecked | Same as above. Reduces MQTT traffic, especially important for remote users on high-latency connections. |

### Unity Settings (`RenderFusion` Component)

| Setting | Recommended | Why |
|---------|-------------|-----|
| **Max Missed Heartbeats** | `1000000` | For remote users (high RTT), MQTT heartbeats can be delayed enough to cause false disconnects. A large value prevents premature timeout. |

### Selective Remote Rendering

Not all objects need to be rendered remotely. You can use the `remote-render` component on individual persisted objects to control which are rendered by Unity vs. locally in the browser:

```json
"remote-render": { "enabled": true }
```

- **`enabled: true`** — the browser skips loading this object's model; Unity renders it remotely via the compositor
- **`enabled: false`** (or absent) — the browser loads and renders the object locally

This is useful when you want heavy models rendered by Unity but need lightweight interactive objects (buttons, UI elements) to remain local for low-latency interaction.

## Browser URL Parameters

The following [URL parameters](https://docs.arenaxr.org/content/interface/params.html) are useful when viewing a RenderFusion scene in the browser:

| Parameter | Description |
|-----------|-------------|
| `noav` | Disables Jitsi videoconferencing. Recommended when testing RenderFusion — eliminates Jitsi errors and reduces bandwidth. |
| `skipav` | Skips the webcam/microphone setup modal on page load. |
| `atw` | Enable/disable Asynchronous Time Warping for remote rendering (default: `true`). |
| `disableRenderFusion` | Explicitly disable RenderFusion — the browser renders everything locally. |

Example URL for RenderFusion testing:
```
https://arenaxr.org/public/my-scene/?noav=1&skipav=1
```

## Alternate Server Usage

In addition to the above, you may deploy your own ARENA webserver if you do not wish to use our test deployment server at [arenaxr.org](https://arenaxr.org). You can follow our setup guidelines in [arenaxr/arena-services-docker](https://github.com/arenaxr/arena-services-docker), to run your own server.

## Samples

### Universal Render Pipeline (recommended)
Open `Samples~/Universal`.

### Built-in Render Pipeline (deprecated)
The `Samples~/BuiltIn` project uses Unity's Built-in Render Pipeline and is deprecated. Use the URP sample for new projects.

### High Definition Render Pipeline
Open `Samples~/HighDefinition`.

## Quick Start

Open `Samples~/Universal` using Unity Hub, then open `Assets/Scenes/MyScene.unity`. Select `ARENARenderFusionURP` in the Hierarchy, configure the ARENA namespace and scene on `ArenaClientScene`, press Play, and open the matching ARENA scene in Chrome.

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
