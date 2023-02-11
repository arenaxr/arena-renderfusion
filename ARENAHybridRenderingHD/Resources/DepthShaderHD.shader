Shader "Hidden/Shader/DepthShaderHD"
{
    SubShader
    {
        Pass
        {
            Name "DepthShaderHD"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment CustomPostProcess
            #pragma vertex Vert

            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone vulkan metal switch

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

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
            TEXTURE2D(_DepthTexture);

            float4 CustomPostProcess(Varyings i) : SV_Target
            {
                float4 col;
                uint2 positionSS;
                if (i.uv.x > 1.0/2.0)
                {
                    positionSS = float2(i.uv.x * 2.0 - 1.0, i.uv.y) * _ScreenSize.xy;
                    float depth = LoadCameraDepth(positionSS);
                    depth = Linear01Depth(depth, _ZBufferParams);
                    col.xyz = 50 * depth;
                }
                else
                {
                    positionSS = float2(i.uv.x * 2.0, i.uv.y) * _ScreenSize.xy;
                    col.xyz = LOAD_TEXTURE2D_X(_MainTex, positionSS).xyz;
                }

                return col;
            }

            ENDHLSL
        }
    }

    Fallback Off
}
