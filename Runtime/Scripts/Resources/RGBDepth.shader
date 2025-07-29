Shader "Hidden/RGBDepth"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RightEyeTex ("RightEyeTexture", 2D) = "white" {}
        _HasRightEyeTex ("HasRightEyeTex", Integer) = 0
        _FrameID ("FrameID", Integer) = 0
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
            int _FrameID;

            sampler2D _CameraDepthNormalsTexture;
            sampler2D _CameraDepthTexture;

            uniform float4 _MainTex_TexelSize;

            // float4x4 UNITY_MATRIX_IV;

            fixed4 RGBDepthSideBySideSingle(v2f i)
            {
                fixed4 col;
                if (i.uv.x <= 0.5)
                {
                    col = tex2D(_MainTex, float2(i.uv.x + 0.25, i.uv.y));
                }
                else
                {
                    float xCoord = i.uv.x - 0.25;
                    // float4 NormalDepth;
                    // DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, float2(xCoord, i.uv.y)), NormalDepth.w, NormalDepth.xyz);
                    // col.rgb = NormalDepth.w;

                    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float2(xCoord, i.uv.y));
                    depth = 50 * Linear01Depth(depth);
                    col.rgb = depth;

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
                 *          Source RGB or Depth texture
                 */
                if (i.uv.x <= 0.25)
                {
                    float xCoord = 2.0 * i.uv.x + 0.25;
                    col = tex2D(_MainTex, float2(xCoord, i.uv.y));
                }
                if (0.25 < i.uv.x && i.uv.x <= 0.5)
                {
                    float xCoord = 2.0 * (i.uv.x - 0.25) + 0.25;
                    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float2(xCoord, i.uv.y));
                    depth = 50 * Linear01Depth(depth);
                    col.rgb = depth;
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
                 *          Source RGB texture                   Source Depth texture
                 */
                // Note: _RightEyeTex should already have the standard RGB-Depth tiling
                if (0.5 < i.uv.x && i.uv.x <= 0.75)
                {
                    float xCoord = 2.0 * (i.uv.x - 0.5);
                    col = tex2D(_RightEyeTex, float2(xCoord, i.uv.y));
                }
                if (0.75 < i.uv.x)
                {
                    float xCoord = 2.0 * (i.uv.x - 0.75) + 0.5;
                    col = tex2D(_RightEyeTex, float2(xCoord, i.uv.y));
                }

                return col;
            }

            fixed4 RGBSideBySideDual(v2f i)
            {
                fixed4 col;
                if (i.uv.x <= 0.5)
                {
                    float xCoord = i.uv.x;
                    col = tex2D(_MainTex, float2(0.25 + xCoord, i.uv.y));
                }
                // Note: _RightEyeTex should already have the standard RGB tiling
                else
                {
                    float xCoord = i.uv.x - 0.5;
                    col = tex2D(_RightEyeTex, float2(0.25 + xCoord, i.uv.y));
                }
                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;
                if (_HasRightEyeTex == 0)
                    col = RGBDepthSideBySideSingle(i); // tex2D(_MainTex, i.uv);
                else
                    col = RGBDepthSideBySideDual(i);

                int width = _MainTex_TexelSize.z;
                int height = _MainTex_TexelSize.w;
                int x = i.uv.x * width;
                int y = (1 - i.uv.y) * height;
                if ((width - 32 < x && x <= width) && (0 <= y && y <= 16)) {
                    // x = x - (width - 32);
                    if ((_FrameID >> x) & 1)
                        col.gb = 1;
                    else
                        col.gb = 0;
                }

                return col;
            }
            ENDCG
        }
    }
}
