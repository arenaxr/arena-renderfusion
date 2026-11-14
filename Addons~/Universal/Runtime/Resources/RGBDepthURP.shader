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
            #include "Packages/io.conix.arena.renderfusion/Runtime/Scripts/Resources/RenderFusionPacking.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            TEXTURE2D_X(_CameraColorTexture);
            SAMPLER(sampler_CameraColorTexture);

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            int _FrameID;

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = RenderFusionSourceUV(input.texcoord);
                float4 col = float4(0, 0, 0, 1);
                if (input.texcoord.x <= 0.5)
                {
                    col.rgb = SAMPLE_TEXTURE2D_X(_CameraColorTexture, sampler_CameraColorTexture, uv).rgb;
                }
                else
                {
                    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
                    col.rgb = RenderFusionEncodeEyeDepthMeters(LinearEyeDepth(depth, _ZBufferParams), depth);
                }

                return RenderFusionEncodeFrameId(col, (int)input.positionCS.x, input.texcoord.y, (int)_ScreenSize.x, (int)_ScreenSize.y, _FrameID);
            }
            ENDHLSL
        }
    }
}
