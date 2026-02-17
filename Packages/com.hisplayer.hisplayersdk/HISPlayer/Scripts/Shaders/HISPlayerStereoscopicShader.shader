Shader "HISPlayer/HISPlayerStereoscopicShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Toggle] _FlipVertically("Flip Vertically", Float) = 1
        [Enum(Left Right, 0, Top Bottom, 1)] _Layout("3D Layout", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            bool _FlipVertically;
            int _Layout;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);

                if (_FlipVertically > 0.5)
                    uv.y = 1.0 - uv.y;

                if (_Layout == 0)
                {
                    uv.x = uv.x * 0.5 + unity_StereoEyeIndex * 0.5;
                }
                else if (_Layout == 1)
                {
                    uv.y = uv.y * 0.5 + (1 - unity_StereoEyeIndex) * 0.5;
                }

                o.uv = uv;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

            #if !UNITY_COLORSPACE_GAMMA
                col.rgb = GammaToLinearSpace(col.rgb);
            #endif

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}