Shader "L1VE/HISPlayer360Shader With Radial Blur" {
Properties {
    _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
    [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
    _Rotation ("Rotation", Range(0, 360)) = 0
    [Toggle] _FlipVertically("Flip Vertically", Float) = 1
    [NoScaleOffset] _MainTex ("Spherical  (HDR)", 2D) = "grey" {}
    [KeywordEnum(6 Frames Layout, Latitude Longitude Layout)] _Mapping("Mapping", Float) = 1
    [Enum(360 Degrees, 0, 180 Degrees, 1)] _ImageType("Image Type", Float) = 0
    [Toggle] _MirrorOnBack("Mirror on Back", Float) = 0
    [Enum(None, 0, Left Right, 1, Top Bottom, 2)] _Layout("3D Layout", Float) = 0
	_RadialBlurCenter("Radial Blur Center", Vector) = (0, 0, 1, 0)
	_RadialBlurAngularDistance("Radial Blur Sample Distance (radians)", Float) = 0.05
	_RadialBlurAmount("Radial Blur Amount", Range(0, 1)) = 0.5
	[Toggle(_APPLY_RADIAL_BLUR)] _ApplyRadialBlur("Apply Radial Blur", Float) = 0
	_FadeToColor("Color to fade to. Only rgb is used, alpha is ignored", Color) = (1, 1, 1, 0)
	_FadeToColorAmount("Fading amount between video and color (0: video, 1: color)", Range(0, 1)) = 0
	[NoScaleOffset] _FrameBlurTex("Blurred copy of the frame, same layout as _MainTex", 2D) = "black" {}
	_FrameBlurAmount("Frame Blur Amount (0: sharp, 1: blurred)", Range(0, 1)) = 0
	[Toggle(_APPLY_FRAME_BLUR)] _ApplyFrameBlur("Apply Frame Blur", Float) = 0
}

SubShader {
    Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
    Cull Off ZWrite Off

    Pass {

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 2.0
        #pragma multi_compile_local __ _MAPPING_6_FRAMES_LAYOUT
		#pragma multi_compile_local __ _APPLY_RADIAL_BLUR
		#pragma multi_compile_local __ _APPLY_FRAME_BLUR

        #include "UnityCG.cginc"

#if _APPLY_RADIAL_BLUR
		#define _RadialBlurSampleCount 9
#else
		#define _RadialBlurSampleCount 1
#endif

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        half4 _MainTex_HDR;
        half4 _Tint;
        half _Exposure;
        float _Rotation;
        bool _FlipVertically;
#ifndef _MAPPING_6_FRAMES_LAYOUT
        bool _MirrorOnBack;
        int _ImageType;
        int _Layout;
#endif
		float3 _RadialBlurCenter;
		float _RadialBlurAngularDistance;
		float _RadialBlurAmount;

		float4 _FadeToColor;
		float _FadeToColorAmount;

		sampler2D _FrameBlurTex;
		float _FrameBlurAmount;

#ifndef _MAPPING_6_FRAMES_LAYOUT
        inline float2 ToRadialCoords(float3 coords)
        {
            float3 normalizedCoords = normalize(coords);
            float latitude = acos(normalizedCoords.y);
            float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
            float2 sphereCoords = float2(longitude, latitude) * float2(0.5/UNITY_PI, 1.0/UNITY_PI);
            return float2(0.5,1.0) - sphereCoords;
        }

        inline float2 ToRadialCoordsNormalized(float3 normalizedCoords)
        {
            float latitude = acos(normalizedCoords.y);
            float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
            float2 sphereCoords = float2(longitude, latitude) * float2(0.5/UNITY_PI, 1.0/UNITY_PI);
            return float2(0.5,1.0) - sphereCoords;
        }
#endif

#ifdef _MAPPING_6_FRAMES_LAYOUT
        inline float2 ToCubeCoords(float3 coords, float3 layout, float4 edgeSize, float4 faceXCoordLayouts, float4 faceYCoordLayouts, float4 faceZCoordLayouts)
        {
            float3 absn = abs(coords);
            float3 absdir = absn > float3(max(absn.y,absn.z), max(absn.x,absn.z), max(absn.x,absn.y)) ? 1 : 0;
            float3 tcAndLen = mul(absdir, float3x3(coords.zyx, coords.xzy, float3(-coords.xy,coords.z)));
            tcAndLen.xy /= tcAndLen.z;
            bool2 positiveAndVCross = float2(tcAndLen.z, layout.x) > 0;
            tcAndLen.xy *= (positiveAndVCross[0] ? absdir.yx : (positiveAndVCross[1] ? float2(absdir[2],0) : float2(0,absdir[2]))) - 0.5;
            tcAndLen.xy = clamp(tcAndLen.xy, edgeSize.xy, edgeSize.zw);
            float4 coordLayout = mul(float4(absdir,0), float4x4(faceXCoordLayouts, faceYCoordLayouts, faceZCoordLayouts, faceZCoordLayouts));
            tcAndLen.xy = (tcAndLen.xy + (positiveAndVCross[0] ? coordLayout.xy : coordLayout.zw)) * layout.yz;
            return tcAndLen.xy;
        }
#endif

        float3 RotateAroundYInDegrees (float3 vertex, float degrees)
        {
            float alpha = degrees * UNITY_PI / 180.0;
            float sina, cosa;
            sincos(alpha, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float3(mul(m, vertex.xz), vertex.y).xzy;
        }

        struct appdata_t {
            float4 vertex : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f {
            float4 vertex : SV_POSITION;
            float3 texcoord : TEXCOORD0;
#ifdef _MAPPING_6_FRAMES_LAYOUT
            float3 layout : TEXCOORD1;
            float4 edgeSize : TEXCOORD2;
            float4 faceXCoordLayouts : TEXCOORD3;
            float4 faceYCoordLayouts : TEXCOORD4;
            float4 faceZCoordLayouts : TEXCOORD5;
#else
            float2 image180ScaleAndCutoff : TEXCOORD1;
            float4 layout3DScaleAndOffset : TEXCOORD2;
#endif
            UNITY_VERTEX_OUTPUT_STEREO
        };

        v2f vert (appdata_t v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
            o.vertex = UnityObjectToClipPos(rotated);
            o.texcoord = v.vertex.xyz;
#ifdef _MAPPING_6_FRAMES_LAYOUT
            float sourceAspect = float(_MainTex_TexelSize.z) / float(_MainTex_TexelSize.w);
            bool3 aspectTest =
                sourceAspect >
                float3(1.0, 1.0f / 6.0f + (3.0f / 4.0f - 1.0f / 6.0f) / 2.0f, 6.0f / 1.0f + (4.0f / 3.0f - 6.0f / 1.0f) / 2.0f);
            if (aspectTest[0])
            {
                if (aspectTest[2])
                {
                    o.faceXCoordLayouts = float4(0.5,0.5,1.5,0.5);
                    o.faceYCoordLayouts = float4(2.5,0.5,3.5,0.5);
                    o.faceZCoordLayouts = float4(4.5,0.5,5.5,0.5);
                    o.layout = float3(-1,1.0/6.0,1.0/1.0);
                }
                else
                {
                    o.faceXCoordLayouts = float4(2.5,1.5,0.5,1.5);
                    o.faceYCoordLayouts = float4(1.5,2.5,1.5,0.5);
                    o.faceZCoordLayouts = float4(1.5,1.5,3.5,1.5);
                    o.layout = float3(-1,1.0/4.0,1.0/3.0);
                }
            }
            else
            {
                if (aspectTest[1])
                {
                    o.faceXCoordLayouts = float4(2.5,2.5,0.5,2.5);
                    o.faceYCoordLayouts = float4(1.5,3.5,1.5,1.5);
                    o.faceZCoordLayouts = float4(1.5,2.5,1.5,0.5);
                    o.layout = float3(1,1.0/3.0,1.0/4.0);
                }
                else
                {
                    o.faceXCoordLayouts = float4(0.5,5.5,0.5,4.5);
                    o.faceYCoordLayouts = float4(0.5,3.5,0.5,2.5);
                    o.faceZCoordLayouts = float4(0.5,1.5,0.5,0.5);
                    o.layout = float3(-1,1.0/1.0,1.0/6.0);
                }
            }
            o.edgeSize.xy = _MainTex_TexelSize.xy * 0.5 / o.layout.yz - 0.5;
            o.edgeSize.zw = -o.edgeSize.xy;
#else
            if (_ImageType == 0)
                o.image180ScaleAndCutoff = float2(1.0, 1.0);
            else
                o.image180ScaleAndCutoff = float2(2.0, _MirrorOnBack ? 1.0 : 0.5);
            if (_Layout == 0)
                o.layout3DScaleAndOffset = float4(0,0,1,1);
            else if (_Layout == 1)
                o.layout3DScaleAndOffset = float4(unity_StereoEyeIndex,0,0.5,1);
            else
                o.layout3DScaleAndOffset = float4(0, unity_StereoEyeIndex,1,0.5);
#endif
            return o;
        }

        uniform int _HISPlayerIsVulkan;

		// code taken from https://docs.unity3d.com/Packages/com.unity.shadergraph@6.9/manual/Rotate-About-Axis-Node.html
		void RotateAboutAxisRadians(float3 In, float3 Axis, float Rotation, out float3x3 Out)
		{
			float s = sin(Rotation);
			float c = cos(Rotation);
			float one_minus_c = 1.0 - c;

			Axis = normalize(Axis);
			float3x3 rot_mat = 
			{   one_minus_c * Axis.x * Axis.x + c, one_minus_c * Axis.x * Axis.y - Axis.z * s, one_minus_c * Axis.z * Axis.x + Axis.y * s,
				one_minus_c * Axis.x * Axis.y + Axis.z * s, one_minus_c * Axis.y * Axis.y + c, one_minus_c * Axis.y * Axis.z - Axis.x * s,
				one_minus_c * Axis.z * Axis.x - Axis.y * s, one_minus_c * Axis.y * Axis.z + Axis.x * s, one_minus_c * Axis.z * Axis.z + c
			};
			//Out = mul(rot_mat,  In);
			Out = rot_mat;
		}

		fixed4 frag (v2f i) : SV_Target
		{
			float3 forward = _RadialBlurCenter;
			float3 toVertex = normalize(i.texcoord);
			float3 rotationAxis = cross(forward, toVertex);
			float3x3 mat;

			float dotForward = -saturate(acos(dot(toVertex, forward)) / (3.14159f * 0.5f));
			float angularOffset = dotForward * _RadialBlurAmount * _RadialBlurAngularDistance;
			//return fixed4(dotForward.xxx, 1.0f);

			half4 tex = 0;
			float3 coord = i.texcoord;
			[unroll(_RadialBlurSampleCount)]
			for (int s = 0; s < _RadialBlurSampleCount; ++s)
			{
				RotateAboutAxisRadians(toVertex, rotationAxis, s * angularOffset, mat);
				coord = mul(mat, toVertex);

#ifdef _MAPPING_6_FRAMES_LAYOUT
				float2 tc = ToCubeCoords(coord, i.layout, i.edgeSize, i.faceXCoordLayouts, i.faceYCoordLayouts, i.faceZCoordLayouts);
#else
				float2 tc = ToRadialCoordsNormalized(coord);
				if (tc.x > i.image180ScaleAndCutoff[1])
					return half4(0,0,0,1);

				if(_FlipVertically > 0.5)
					tc.y = 1.0 - tc.y;

				tc.x = fmod(tc.x*i.image180ScaleAndCutoff[0], 1);
				tc = (tc + i.layout3DScaleAndOffset.xy) * i.layout3DScaleAndOffset.zw;
#endif

#ifdef _APPLY_FRAME_BLUR
				tex += lerp(tex2D(_MainTex, tc), tex2D(_FrameBlurTex, tc), _FrameBlurAmount);
#else
				tex += tex2D (_MainTex, tc);
#endif
			}

			tex /= _RadialBlurSampleCount;

  #if !UNITY_COLORSPACE_GAMMA
            if (!_HISPlayerIsVulkan)
            {
                tex.rgb = GammaToLinearSpace(tex.rgb);
            }
  #endif

            half3 c = DecodeHDR (tex, _MainTex_HDR);
            c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb;
            c *= _Exposure;

			c = lerp(c, _FadeToColor.rgb, _FadeToColorAmount);
            return half4(c, 1);
        }
        ENDCG
    }
}


//CustomEditor "HISPlayerShader.HISPlayer360ShaderGUI"
Fallback Off

}