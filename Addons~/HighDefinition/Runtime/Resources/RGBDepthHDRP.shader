Shader "Hidden/RGBDepthHDRP"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"
            #include "Packages/io.conix.arena.renderfusion/Runtime/Scripts/Resources/RenderFusionPacking.hlsl"

            #pragma fragment CustomPostProcess
            #pragma vertex Vert

            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone vulkan metal switch

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.vertex = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            TEXTURE2D_X(_MainTex);
            int _FrameID;

            float4 CustomPostProcess(Varyings i) : SV_Target
            {
                float2 uv = RenderFusionSourceUV(i.uv);
                float2 positionSS = uv * _ScreenSize.xy;
                float4 col = float4(0, 0, 0, 1);
                if (i.uv.x <= 0.5)
                {
                    col.rgb = LOAD_TEXTURE2D_X(_MainTex, positionSS).rgb;
                }
                else
                {
                    float depth = LoadCameraDepth(positionSS);
                    col.rgb = RenderFusionEncodeEyeDepthMeters(LinearEyeDepth(depth, _ZBufferParams), depth);
                }

                return RenderFusionEncodeFrameId(col, (int)i.vertex.x, i.uv.y, (int)_ScreenSize.x, (int)_ScreenSize.y, _FrameID);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
