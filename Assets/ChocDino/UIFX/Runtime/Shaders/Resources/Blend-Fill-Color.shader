Shader "Hidden/ChocDino/UIFX/Blend-Fill-Color"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_FillColor    ("Fill Color", Color) = (1, 1, 1, 1)
		_FillColorA  ("Fill Color A", Color) = (1, 1, 1, 1)
		_FillColorB  ("Fill Color B", Color) = (0, 0, 0, 1)
		_FillColorTL  ("Fill Color Top Left", Color) = (1, 1, 1, 1)
		_FillColorTR  ("Fill Color Top Right", Color) = (1, 1, 1, 1)
		_FillColorBL  ("Fill Color Bottom Left", Color) = (0, 0, 0, 1)
		_FillColorBR  ("Fill Color Bottom Right", Color) = (0, 0, 0, 1)
		_ScreenRect   ("Screen Rect", Vector) = (0, 0, 1, 1)
		_ColorScaleBias ("Color Scale Bias", Vector) = (1, 0, 0, 0)
		_BlendMode    ("Blend Mode", Float) = 1.0
		_Strength     ("Strength", Float) = 1.0

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local_fragment _ MODE_HORIZONTAL MODE_VERTICAL MODE_CORNERS
	#pragma multi_compile_local_fragment _ BLEND_FORCEGAMMASPACE

	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"
	#include "Common/BlendModes.cginc"

	uniform half4 _FillColor;
	uniform half4 _FillColorA;
	uniform half4 _FillColorB;
	uniform half4 _FillColorTL;
	uniform half4 _FillColorTR;
	uniform half4 _FillColorBL;
	uniform half4 _FillColorBR;
	uniform half2 _ColorScaleBias;
	uniform half4 _ScreenRect;
	uniform int _BlendMode;
	uniform half _Strength;

	fixed4 frag(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		fixed4 result = tex2D(_ResultTex, i.uv.xy);

		i.uv.xy = i.uv.xy * _ScreenRect.zw + _ScreenRect.xy;

		// NOTE: Interpolation generally looks better in a perceptually uniform colorspace (not-linear),
		// so may want to add something like this in the future.
		/*if (!IsGammaSpace())
		{
			//i.uv.xy = GammaToLinearSpace(i.uv.xyy).xy;
			//fill.rgb = GammaToLinearSpace(fill);
		}*/

		#if MODE_CORNERS
		fixed4 fill = CornerGradient(_FillColorTL, _FillColorTR, _FillColorBL, _FillColorBR, i.uv.xy, _ColorScaleBias.x);
		#elif MODE_HORIZONTAL
		fixed4 fill = EdgeGradient(_FillColorA, _FillColorB, i.uv.x, _ColorScaleBias.x, _ColorScaleBias.y);
		#elif MODE_VERTICAL
		fixed4 fill = EdgeGradient(_FillColorB, _FillColorA, i.uv.y, _ColorScaleBias.x, 1.0-_ColorScaleBias.y);
		#else
		fixed4 fill = _FillColor;
		#endif

		fill = ToPremultiplied(fill);
		half4 blend = DoBlend(_BlendMode, result, fill);
		fixed4 color = lerp(result, blend, _Strength);

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
			Name "Blend-Fill-Color"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}