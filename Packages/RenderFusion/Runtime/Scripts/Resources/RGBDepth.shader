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
            #include "RenderFusionPacking.hlsl"

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

            sampler2D _CameraDepthTexture;

            uniform float4 _MainTex_TexelSize;

            fixed4 RGBDepthSideBySideSingle(float2 packedUV)
            {
                float2 uv = RenderFusionSourceUV(packedUV);
                fixed4 col = fixed4(0, 0, 0, 1);
                if (packedUV.x <= 0.5)
                {
                    col = tex2D(_MainTex, uv);
                }
                else
                {
                    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                    col.rgb = RenderFusionEncodeEyeDepthMeters(LinearEyeDepth(depth), depth);
                }

                return col;
            }

            fixed4 RGBDepthSideBySideDual(v2f i)
            {
                if (i.uv.x <= 0.5)
                    return RGBDepthSideBySideSingle(float2(2.0 * i.uv.x, i.uv.y));

                // The right camera already produced [color | depth]; copy both halves.
                return tex2D(_RightEyeTex, float2(2.0 * (i.uv.x - 0.5), i.uv.y));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;
                if (_HasRightEyeTex == 0)
                    col = RGBDepthSideBySideSingle(i.uv);
                else
                    col = RGBDepthSideBySideDual(i);

                return RenderFusionEncodeFrameId(col, (int)i.vertex.x, i.uv.y, (int)_MainTex_TexelSize.z, (int)_MainTex_TexelSize.w, _FrameID);
            }
            ENDCG
        }
    }
}
