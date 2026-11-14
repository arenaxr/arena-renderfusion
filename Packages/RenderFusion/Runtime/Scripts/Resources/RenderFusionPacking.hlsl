#ifndef ARENA_RENDERFUSION_PACKING_INCLUDED
#define ARENA_RENDERFUSION_PACKING_INCLUDED

// ARENA's unchanged compositor decodes red / 50 against its 10000 m far plane:
// one video unit represents 200 metres. Encode physical eye-space distance so
// renderer clip planes and XR projection rounding cannot change that wire format.
static const float RenderFusionDepthRangeMeters = 200.0;

float RenderFusionEncodeEyeDepthMeters(float eyeDepthMeters, float rawDepth)
{
    // A cleared depth buffer represents no geometry, not a surface at the
    // renderer's far plane. Keep ARENA's white-background sentinel even if that
    // plane is closer than the transmitted 200 m range.
#if UNITY_REVERSED_Z
    if (rawDepth == 0.0)
#else
    if (rawDepth == 1.0)
#endif
        return 1.0;

    return eyeDepthMeters / RenderFusionDepthRangeMeters;
}

// Each eye is rendered at twice its transmitted color width. Both output halves
// sample the middle half of that view: [color | depth]. Camera viewports arrange
// two such eye images as [left color | left depth | right color | right depth].
float2 RenderFusionSourceUV(float2 packedUV)
{
    return float2(packedUV.x + (packedUV.x <= 0.5 ? 0.25 : -0.25), packedUV.y);
}

float4 RenderFusionEncodeFrameId(float4 color, int pixelX, float imageUVY, int width, int height, int frameID)
{
    // ARENA decodes the image top (UV y=1). Unity's full-screen blits and WebRTC's
    // capture copy already handle texture orientation: native framebuffer origins
    // do not identify that image edge consistently across graphics backends.
    int topPixelY = (int)((1.0 - imageUVY) * height);
    // Preserve the browser's legacy absolute-x, modulo-32 bit ordering and bounds.
    // Explicit masking defines the shift on Metal/SPIR-V as well as Direct3D.
    if (width - 32 < pixelX && pixelX <= width && 0 <= topPixelY && topPixelY <= 16)
        color.gb = ((frameID >> (pixelX & 31)) & 1) ? 1.0 : 0.0;
    return color;
}

#endif
