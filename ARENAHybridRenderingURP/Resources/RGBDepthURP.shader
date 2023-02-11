Shader "Hidden/RGBDepthShaderURP"
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

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float _DualCameras;

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 col;
                if (!_DualCameras)
                {
                    if (input.texcoord.x <= 1.0/2.0)
                    {
                        float xcoord = input.texcoord.x;
                        float2 uv = float2(2.0 * xcoord, input.texcoord.y);
                        col.rgb = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv).rgb;
                    }
                    else
                    {
                        float xcoord = input.texcoord.x - 1.0/2.0;
                        float2 uv = float2(2.0 * xcoord, input.texcoord.y);
                        float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
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
                    *         RGB or depth texture
                    */
                    if (input.texcoord.x <= 1.0/2.0)
                    {
                        float xcoord = input.texcoord.x;
                        float2 uv = float2(1.0/4.0 + xcoord, input.texcoord.y);
                        col.rgb = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv).rgb;
                    }
                    else
                    {
                        float xcoord = input.texcoord.x - 1.0/2.0;
                        float2 uv = float2(1.0/4.0 + xcoord, input.texcoord.y);
                        float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                        depth = 5 * Linear01Depth(depth, _ZBufferParams);
                        col.rgb = depth;
                    }
                }

                return col;
            }
            ENDHLSL
        }
    }
}
