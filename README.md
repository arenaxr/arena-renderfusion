# ARENA RenderFusion

RenderFusion with [ARENA](https://arenaxr.org/) and Unity!

<img alt="demo image" src="Documentation~/images/demo.png">

See a demo video [here](https://www.youtube.com/watch?v=6mA4k9myuOM)!

## Requirements

We implement a custom layer on top of Unity's [WebRTC package](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/index.html). See their [Requirements](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/requirements.html).

## Usage

See [Installation](Documentation~/install.md) section. *For AR/VR clients, it is reccomeneded you use this package in __URP__ for the best performance.*

Once you have the relevant packages installed, press Play on the Unity Editor or build the Unity application, then enter the ARENA scene (`<Host Address>/<Namespace Name>/<Scene Name>`) on a web browser!

## Samples

### Universal Render Pipeline (recommended)
Open `Samples~/URP`.

### Standard Render Pipeline
Open `Samples~/SRP`.

### High Definition Render Pipeline
Open `Samples~/HDRP`.

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
