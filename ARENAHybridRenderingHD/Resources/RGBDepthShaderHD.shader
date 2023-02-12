Shader "Hidden/RGBDepthShaderHD"
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
            TEXTURE2D_X(_DepthTexture);

            float _DualCameras;

            float4 CustomPostProcess(Varyings i) : SV_Target
            {
                float4 col;
                float xcoord;
                uint2 positionSS;

                if (!_DualCameras)
                {
                    if (i.uv.x <= 1.0/2.0)
                    {
                        xcoord = i.uv.x;
                        positionSS = float2(xcoord * 2.0, i.uv.y) * _ScreenSize.xy;
                        col.rgb = LOAD_TEXTURE2D_X(_MainTex, positionSS).rgb;
                    }
                    else
                    {
                        xcoord = i.uv.x - 1.0/2.0;
                        positionSS = float2(xcoord * 2.0, i.uv.y) * _ScreenSize.xy;
                        float depth = LoadCameraDepth(positionSS);
                        depth = Linear01Depth(depth, _ZBufferParams);
                        col.rgb = 50 * depth;
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
                    if (i.uv.x <= 1.0/2.0)
                    {
                        xcoord = i.uv.x;
                        positionSS = float2(1.0/4.0 + xcoord, i.uv.y) * _ScreenSize.xy;
                        col.rgb = LOAD_TEXTURE2D_X(_MainTex, positionSS).rgb;
                    }
                    else
                    {
                        xcoord = i.uv.x - 1.0/2.0;
                        positionSS = float2(1.0/4.0 + xcoord, i.uv.y) * _ScreenSize.xy;
                        float depth = LoadCameraDepth(positionSS);
                        depth = Linear01Depth(depth, _ZBufferParams);
                        col.rgb = 50 * depth;
                    }
                }

                return col;
            }

            ENDHLSL
        }
    }

    Fallback Off
}
