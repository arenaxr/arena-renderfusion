# ARENA RenderFusion

High-quality remote/split rendering on the web! Stream Unity scenes into the browser using RenderFusion with [ARENA](https://arenaxr.org/)!

RenderFusion is a form of "remote rendering", which uses a nearby powerful machine to render 3D content and streams the rendering results to a web browser. RenderFusion also employs "split rendering", which renders some portions of the 3D scene locally on the browser to reduce latency.

Note: this is an *experimental* ARENA feature, but is deployed on the main branch, so it works out the box!

<img alt="demo image" src="Documentation~/images/demo.png">

(Scene from https://assetstore.unity.com/packages/essentials/tutorial-projects/book-of-the-dead-environment-hdrp-121175).

See demo videos: [RenderFusion](https://www.youtube.com/watch?v=6mA4k9myuOM) and [Volumetric Capture and Streaming using RenderFusion](https://www.youtube.com/watch?v=561-RQ1zVc4)!

## Requirements

We implement a custom layer on top of Unity's [WebRTC package](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/index.html). See their [Requirements](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/requirements.html).

## Usage

See [Installation](Documentation~/install.md) section. *For AR/VR clients, it is HIGHLY reccomeneded you use this package in __URP__ for the best performance.*

Once you have the relevant packages installed, press Play on the Unity Editor or build the Unity application, then enter the ARENA scene (`https://arenaxr.org/<ARENA User Name>/<Scene Name>`) on a web browser!

## Samples

### Universal Render Pipeline (recommended)
Open `Samples~/URP`.

### Standard Render Pipeline
Open `Samples~/SRP`.

### High Definition Render Pipeline
Open `Samples~/HDRP`.

## Quick Start

For a quick start, simply open `Samples~/URP` using the Unity Hub. Click on the `ARENARenderFusion` Game Object, change the Scene Name to your favorite ARENA scene, then press Play!

## License
See the [LICENSE](LICENSE) file.

If you find this project helpful for any research-related purposes, please consider citing our paper:
```
@inproceedings{renderfusion,
  author = {Lu, Edward and Bharadwaj, Sagar and Dasari, Mallesham and Smith, Connor and Seshan, Srinivasan and Rowe, Anthony},
  booktitle = {2023 International Symposium on Mixed and Augmented Reality (ISMAR)},
  title = {RenderFusion: Balancing Local and Remote Rendering for Interactive 3D Scenes},
  year = {2023}
}
```
