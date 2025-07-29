Shader "Hidden/RGBDepthURP"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            TEXTURE2D_X(_CameraColorTexture);
            SAMPLER(sampler_CameraColorTexture);

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            int _HasStereoCameras;
            int _FrameID;

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 col;
                if (!_HasStereoCameras)
                {
                    if (input.texcoord.x <= 0.5)
                    {
                        float xCoord = input.texcoord.x;
                        float2 uv = float2(0.25 + xCoord, input.texcoord.y);
                        col.rgb = SAMPLE_TEXTURE2D_X(_CameraColorTexture, sampler_CameraColorTexture, uv).rgb;
                    }
                    else
                    {
                        float xCoord = input.texcoord.x - 0.5;
                        float2 uv = float2(0.25 + xCoord, input.texcoord.y);
                        float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
                        depth = 5 * Linear01Depth(depth, _ZBufferParams);
                        col.rgb = depth;
                    }
                }
                else
                {
                    // here, we want the "middle half" of the RGB and depth texture, so offset by 1/4
                    /*
                    *  ------------------------------------
                    * |        |                 |         |
                    * |        |                 |         |
                    * |        |      we         |         |
                    * |        |     want        |         |
                    * |        |     this        |         |
                    * |        |                 |         |
                    * |        |                 |         |
                    * |        |                 |         |
                    *  ------------------------------------
                    *          Source RGB or Depth texture
                    */
                    if (input.texcoord.x <= 0.5)
                    {
                        float xCoord = input.texcoord.x;
                        float2 uv = float2(0.25 + xCoord, input.texcoord.y);
                        col.rgb = SAMPLE_TEXTURE2D_X(_CameraColorTexture, sampler_CameraColorTexture, uv).rgb;
                    }
                    else
                    {
                        float xCoord = input.texcoord.x - 0.5;
                        float2 uv = float2(0.25 + xCoord, input.texcoord.y);
                        float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
                        depth = 5 * Linear01Depth(depth, _ZBufferParams);
                        col.rgb = depth;
                    }
                }

                int width = _ScreenSize.x;
                int height =  _ScreenSize.y;
                int x = input.texcoord.x * width;
                int y = (1 - input.texcoord.y) * height;
                if ((width - 32 < x && x <= width) && (0 <= y && y <= 16)) {
                    // x = x - (width - 32);
                    if ((_FrameID >> x) & 1)
                        col.gb = 1;
                    else
                        col.gb = 0;
                }

                return col;
            }
            ENDHLSL
        }
    }
}
