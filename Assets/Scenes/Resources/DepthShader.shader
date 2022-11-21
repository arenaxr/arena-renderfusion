Shader "Hidden/DepthShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            sampler2D _CameraDepthNormalsTexture;
            // sampler2D _CameraDepthTexture;

            // float4x4 UNITY_MATRIX_IV;

            // float3 HSV2RGB(float3 HSV) {
            //     float3 RGB = HSV.z;

            //     float var_h = HSV.x * 6;
            //     float var_i = floor(var_h);   // Or ... var_i = floor( var_h )
            //     float var_1 = HSV.z * (1.0 - HSV.y);
            //     float var_2 = HSV.z * (1.0 - HSV.y * (var_h-var_i));
            //     float var_3 = HSV.z * (1.0 - HSV.y * (1-(var_h-var_i)));
            //     if      (var_i == 0) { RGB = float3(HSV.z, var_3, var_1); }
            //     else if (var_i == 1) { RGB = float3(var_2, HSV.z, var_1); }
            //     else if (var_i == 2) { RGB = float3(var_1, HSV.z, var_3); }
            //     else if (var_i == 3) { RGB = float3(var_1, var_2, HSV.z); }
            //     else if (var_i == 4) { RGB = float3(var_3, var_1, HSV.z); }
            //     else                 { RGB = float3(HSV.z, var_1, var_2); }

            //    return (RGB);
            // }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;
                if (i.uv.x > 1.0/2.0) {
                    float4 NormalDepth;
                    DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, float2(i.uv.x * 2.0 - 1.0, i.uv.y)), NormalDepth.w, NormalDepth.xyz);
                    // col.rgb = NormalDepth.w;

                    // float depth = tex2D(_CameraDepthTexture, float2(i.uv.x * 2.0 - 1.0, i.uv.y)).r;
                    float depth = NormalDepth.w;
                    depth = Linear01Depth(depth);
                    // depth = depth * _ProjectionParams.z;

#ifdef SHADER_API_METAL
                    col.rgb = 1.0 - depth;
#else
                    col.rgb = depth;
#endif

                    // float depth1 = Linear01Depth(depth);
                    // float depth1 = depth * _ProjectionParams.z;
                    // if (depth1 >= _ProjectionParams.z)
                    //     col.g = 1.0;
                    // else
                    //     col.g = 0.0;
                }
                else {
                    col = tex2D(_MainTex, float2(i.uv.x * 2.0, i.uv.y));
                }

                return col;
            }
            ENDCG
        }
    }
}
