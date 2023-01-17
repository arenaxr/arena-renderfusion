Shader "Hidden/RGBDepthShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RightEyeTex ("RightEyeTexture", 2D) = "white" {}
        _HasRightEyeTex ("HasRightEyeTex", Integer) = 0
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
            sampler2D _RightEyeTex;
            int _HasRightEyeTex;

            sampler2D _CameraDepthNormalsTexture;
            sampler2D _CameraDepthTexture;

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

            fixed4 RGBDepthSideBySideSingle(v2f i)
            {
                fixed4 col;
                if (i.uv.x <= 1.0/2.0)
                {
                    col = tex2D(_MainTex, float2(i.uv.x * 2.0, i.uv.y));
                }
                else
                {
                    float xcoord = i.uv.x - 1.0/2.0;
                    // float4 NormalDepth;
                    // DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, float2(xcoord, i.uv.y)), NormalDepth.w, NormalDepth.xyz);
                    // col.rgb = NormalDepth.w;

                    float depth = tex2D(_CameraDepthTexture, float2(2.0 * xcoord, i.uv.y)).r;
                    depth = Linear01Depth(depth);
                    col.rgb = 50 * depth;

                    // float depth1 = Linear01Depth(depth);
                    // float depth1 = depth * _ProjectionParams.z;
                    // if (depth1 >= _ProjectionParams.z)
                    //     col.g = 1.0;
                    // else
                    //     col.g = 0.0;
                }

                return col;
            }

            fixed4 RGBDepthSideBySideDual(v2f i)
            {
                fixed4 col;
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
                if (i.uv.x <= 1.0/4.0)
                {
                    col = tex2D(_MainTex, float2(1.0/4.0 + 2.0 * i.uv.x, i.uv.y));
                }
                if (1.0/4.0 < i.uv.x && i.uv.x <= 1.0/2.0)
                {
                    float xcoord = i.uv.x - 1.0/4.0;
                    float depth = tex2D(_CameraDepthTexture, float2(1.0/4.0 + 2.0 * xcoord, i.uv.y)).r;
                    depth = Linear01Depth(depth);
                    col.rgb = 50 * depth;
                }
                // here, we want the "middle half" of each half, we offset by 1/8
                /*
                 *  ------------------------------------ ------------------------------------
                 * |        |                 |         |        |                 |         |
                 * |        |                 |         |        |                 |         |
                 * |        |      we         |         |        |      we         |         |
                 * |        |     want        |         |        |     want        |         |
                 * |        |     this        |         |        |     this        |         |
                 * |        |                 |         |        |                 |         |
                 * |        |                 |         |        |                 |         |
                 * |        |                 |         |        |                 |         |
                 *  ------------------------------------ ------------------------------------
                 *                  RGB texture                     depth texture
                 */
                // Note: _RightEyeTex should already have the standard RGB-Depth tiling
                if (1.0/2.0 < i.uv.x && i.uv.x <= 3.0/4.0)
                {
                    float xcoord = i.uv.x - 1.0/2.0;
                    col = tex2D(_RightEyeTex, float2(1.0/8.0 + xcoord, i.uv.y));
                }
                if (3.0/4.0 < i.uv.x)
                {
                    float xcoord = i.uv.x - 3.0/4.0;
                    col = tex2D(_RightEyeTex, float2(1.0/8.0 + xcoord + 1.0/2.0, i.uv.y));
                }

                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;
                if (_HasRightEyeTex == 0)
                {
                    col = RGBDepthSideBySideSingle(i);
                }
                else
                {
                    col = RGBDepthSideBySideDual(i);
                }

                return col;
            }
            ENDCG
        }
    }
}
