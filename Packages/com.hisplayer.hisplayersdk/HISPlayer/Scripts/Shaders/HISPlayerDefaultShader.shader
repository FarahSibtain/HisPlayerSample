Shader"HISPlayer/HISPlayerDefaultShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Toggle] _FlipVertically("Flip Vertically", Float) = 1
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque"}
        LOD 100

        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #pragma multi_compile __ COLOR_CORRECTION

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            bool _FlipVertically;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                if (_FlipVertically > 0.5)
                    o.uv.y = 1.0 - o.uv.y;

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            uniform int _HISPlayerIsVulkan;

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
  #if !UNITY_COLORSPACE_GAMMA
                if (!_HISPlayerIsVulkan)
                {
                    col.rgb = GammaToLinearSpace(col.rgb); // Remove gamma correction
                }
  #endif
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
