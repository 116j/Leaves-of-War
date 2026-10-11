Shader "Hidden/ChocDino/UIFX/Blend-Dissolve"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}
		[PerRendererData] _FillTex ("Fill Texture", 2D) = "black" {}
		[PerRendererData] _DirMaskTex("Dir Mask Texture", 2D) = "black" {}
		[PerRendererData] _EdgeTex ("Edge Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask ("Color Mask", Float) = 15

		_Dissolve ("Dissolve", Vector) = (0.0, 0.1, 0.0, 0.0)
		_DirectionStrength ("Direction Strength", Float) = 1.0
		_EdgeColor ("Edge Color", Vector) = (1.0, 1.0, 1.0, 1.0)
		_EdgeEmissive ("Edge Emissive", Float) = 1.0
		_InvertFactor ("Invert Factor", Vector) = (0.0, -1.0, 0.0, 0.0)
		_ScreenRect("Screen Rect", Vector) = (0, 0, 1, 1)
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_vertex _ SCREEN_SPACE
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local_fragment _ DIR_TEXTURE DIR_PROCEDURAL
	#pragma multi_compile_local_fragment _ EDGE_COLOR EDGE_RAMP

	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	struct v2fDissolve
	{
		float4 vertex : SV_POSITION;
		float4 uv : TEXCOORD0;
		float4 color : TEXCOORD1;
		float4 directionUV : TEXCOORD2;
		#ifdef UNITY_UI_CLIP_RECT
		float4 mask : TEXCOORD3;
		#endif

		UNITY_VERTEX_OUTPUT_STEREO // VR SUPPORT
	};

	uniform float3 _Dissolve;
	uniform sampler2D _FillTex;
	uniform float4 _FillTex_ST;
	uniform float4 _FillTex_TexelSize;
	uniform sampler2D _DirMaskTex;
	uniform float _DirectionStrength;
	uniform float4 _EdgeColor;
	uniform float _EdgeEmissive;
	uniform sampler2D _EdgeTex;
	uniform float2 _InvertFactor;
	uniform float _MaskRange;
	uniform float _Direction;
	uniform half4 _ScreenRect;
	uniform half4 _ScreenSpaceRect;

	v2fDissolve vertDissolve(appdata v)
	{
		v2fDissolve o;

		UNITY_SETUP_INSTANCE_ID(v); // VR SUPPORT
		UNITY_INITIALIZE_OUTPUT(v2fDissolve, o); // VR SUPPORT
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // VR SUPPORT

		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv.xy = TRANSFORM_TEX(v.uv, _ResultTex);

		// Scale and offset for texture aspect ratio
		o.uv.zw = v.uv;
		o.uv.zw = o.uv.zw * _ScreenRect.zw + _ScreenRect.xy;
		o.uv.zw = o.uv.zw * _FillTex_ST.xy + _FillTex_ST.zw;

		#ifdef SCREEN_SPACE
		o.directionUV = ComputeScreenPos(o.vertex);
		o.directionUV.xy /= o.directionUV.w; // Perspective-divide is needed to unify Canvas.RenderMode (Overlay and Camera)
		o.directionUV.z = _ScreenParams.x / _ScreenParams.y;
		#else
		o.directionUV = float4(o.uv.xy, 0, 1);
		o.directionUV.z = _ResultTex_TexelSize.z / _ResultTex_TexelSize.w;
		#endif

		o.color = v.color;

		// 2D rect clipping
		#ifdef UNITY_UI_CLIP_RECT
		{
			float2 pixelSize = o.vertex.w;
			pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			#ifdef MASK_SOFTNESS_OLD
			half2 maskSoftness = half2(_MaskSoftnessX, _MaskSoftnessY);
			#else
			half2 maskSoftness = half2(_UIMaskSoftnessX, _UIMaskSoftnessY);
			#endif
			o.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));
		}
		#endif
		
		return o;
	}

	#define DIR_NONE 0
	#define DIR_X 1
	#define DIR_Y 2
	#define DIR_MIRRORX 10
	#define DIR_MIRRORY 11
	#define DIR_DIAGONAL 20
	#define DIR_CIRCLE 30

	float4 fragDissolve(v2fDissolve i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		float4 source = tex2D(_ResultTex, i.uv.xy);

		// Use RGB color channels
		float4 dissolveSource = tex2D(_FillTex, i.uv.zw);
		float mask = (dissolveSource.r + dissolveSource.g + dissolveSource.b) / 3.0;
		mask = abs(_InvertFactor.x - mask);
		float maskRange = _MaskRange;

		#if DIR_TEXTURE || DIR_PROCEDURAL
		{
			float dirMaskLength = 0.0;
			float dirMask = 0.0;

			// Add the extra direction
			#if DIR_TEXTURE
			
			dirMaskLength = 1.0;
			dirMask = tex2D(_DirMaskTex, i.directionUV.xy).r;

			#elif DIR_PROCEDURAL

			float2 aspect = float2(i.directionUV.z, 1.0);
			i.uv.xy = i.directionUV.xy;

			if (_Direction == DIR_X)
			{
				// X
				dirMaskLength = aspect.x;
				dirMask = i.uv.x * aspect.x;
			}
			else if (_Direction == DIR_Y)
			{
				// Y
				dirMaskLength = aspect.y;
				dirMask = i.uv.y * aspect.y;
			}
			else if (_Direction == DIR_MIRRORX)
			{
				// MirrorX
				dirMaskLength = aspect.x;
				dirMask = abs(2.0 * (i.uv.xy - 0.5) * aspect).x;
			}
			else if (_Direction == DIR_MIRRORY)
			{
				// MirrorY
				dirMaskLength = aspect.y;
				dirMask = abs(2.0 * (i.uv.xy - 0.5) * aspect).y;
			}
			else if (_Direction == DIR_DIAGONAL)
			{
				// Diagonal
				dirMaskLength = (aspect.x + aspect.y);
				dirMask = (i.uv.xy.x * aspect.x + i.uv.xy.y * aspect.y);
			}
			else if (_Direction == DIR_CIRCLE)
			{
				// Circle
				dirMaskLength = length(aspect);
				dirMask = length(2.0 * (i.uv.xy - 0.5) * aspect);
			}

			#endif

			dirMask = abs((dirMaskLength * _InvertFactor.y) - dirMask);
			mask += dirMask * _DirectionStrength;
			dirMaskLength *= _DirectionStrength;
			maskRange += dirMaskLength;
		}
		#endif

		// Remap strength from 0..1 range to -softWidth..maskRange + softWidth
		float strength = _Dissolve.z;
		float softWidth = _Dissolve.y;
		float edgeMiddle = lerp(-softWidth, maskRange + softWidth, strength);
		float edgeStart = edgeMiddle - softWidth;
		float edgeEnd = edgeMiddle + softWidth;
		float vv = smoothstep(edgeStart, edgeEnd, mask);

		float4 color = source;

		#if EDGE_COLOR
		float fadeT = 0.1;
		float t = saturate(1.0 - vv);
		float a = 1.0 - saturate(t / fadeT);
		float c = saturate((t - (1.0-fadeT)) / fadeT);
		float b = min(1.0-a, 1.0-c);

		float4 mainColor = color * a;
		float4 middleColor = _EdgeEmissive * _EdgeColor * b;
		float4 fadeColor = 0 * c;

		middleColor *= source.a;

		float4 edgeColor = mainColor+middleColor+fadeColor;
		//edgeColor.a = 0.0;
		//edgeColor *= source.a;
		color = edgeColor;
		color.a = saturate(color.a);		// Need to clamp alpha as it can't go above 1.0 for Hdr otherwise compositing functions will not be correct

		//return edgeColor;
		//return a+b+c;

		//float4 edgeColor = _EdgeColor * source.a * vv;
		//color = lerp(color, edgeColor, step(vv, 0.99))*vv;
		#elif EDGE_RAMP

		float fadeT = 0.01;
		float t = saturate(1.0 - vv);
		float a = 1.0 - saturate(t / fadeT);
		float c = saturate((t - (1.0-fadeT)) / fadeT);
		float b = 1.0-max(a, c);

		float bx = saturate((t - fadeT) / (1.0 - (fadeT * 2.0)));

		float4 mainColor = color * a;
		float4 middleColor = _EdgeEmissive * tex2D(_EdgeTex, float2(bx, 0)) * b;
		//return middleColor;
		float4 fadeColor = 0 * c;

		middleColor *= source.a;

		float4 edgeColor = mainColor+middleColor+fadeColor;
		//edgeColor.a = 1.0;
		//edgeColor *= source.a;
		//return edgeColor;
		color = edgeColor;

		color.a = saturate(color.a);		// Need to clamp alpha as it can't go above 1.0 for Hdr otherwise compositing functions will not be correct

		#else

		color *= vv;

		#endif

		

		// 2D rect clipping
		#ifdef UNITY_UI_CLIP_RECT
		color = ApplyClipRect(color, i.mask);
		#endif

		// Alpha clipping
		#ifdef UNITY_UI_ALPHACLIP
		clip (color.a - 0.001);
		#endif

		color.rgb *= i.color.a;
		color *= i.color;

		return color;
	}

	ENDCG

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
			"OutputsPremultipliedAlpha"="True"
		}

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend One OneMinusSrcAlpha // Premultiplied transparency
		ColorMask [_ColorMask]

		Pass
		{
			Name "Blend-Dissolve"
			CGPROGRAM
			#pragma vertex vertDissolve
			#pragma fragment fragDissolve
			ENDCG
		}
	}
}