Shader "Hidden/ChocDino/UIFX/Blend-DropShadow"
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

		_SourceAlpha ("Source Alpha", Float) = 1
		_ShadowHardness ("Shadow Hardness", Float) = 1
		_ShadowColor ("Shadow Color", Color) = (0, 0, 0, 1)
		_ShadowOffset ("Shadow Offset", Vector) = (0, 0, 0, 0)

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local_fragment __ INSET GLOW CUTOUT

	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	float _SourceAlpha = 1.0;
	float _ShadowHardness = 1.0;
	float4 _ShadowColor = float4(0.0, 0.0, 0.0, 1.0);
	float2 _ShadowOffset = float2(0.02, 0.02);

	float4 fragDropShadow(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		float4 source = tex2D(_SourceTex, i.uv);

		// Note: Need to do this explicit swizzling for console compilers
		float zero = 0.0;
		float4 color = lerp(zero.xxxx, source, _SourceAlpha.xxxx);

		float4 shadowColor = _ShadowColor;
			
		float2 offsetuv = i.uv + _ShadowOffset;
		// Have to check bounds otherwise could repeat samples from the edge
		if (offsetuv.x >= 0.0 && offsetuv.y >= 0.0 && offsetuv.x <= 1.0 && offsetuv.y <= 1.0)
		{
			float4 blur = tex2D(_ResultTex, offsetuv);

			#if (INSET)
			{
				float4 inverse = 1.0 - blur;
				float4 shadow = AlphaComp_In(shadowColor, inverse);
				shadow = saturate(shadow * _ShadowHardness);
				color = AlphaComp_ATop(shadow, color);
			}
			#elif (GLOW)
			{
				float4 shadow = blur * shadowColor;
				shadow = saturate(shadow * _ShadowHardness);
				color = AlphaComp_Over(color, shadow);
			}
			#elif (CUTOUT)
			{
				float4 shadow = AlphaComp_In(shadowColor, blur);
				shadow = saturate(shadow * _ShadowHardness);
				color = AlphaComp_Out(shadow, color);
			}
			#else
			{
				float4 shadow = AlphaComp_In(shadowColor, blur);
				shadow = saturate(shadow * _ShadowHardness);
				color = AlphaComp_Over(color, shadow);
			}
			#endif
		}
		else
		{
			#if (INSET)
			{
				color = shadowColor * _ShadowHardness * source.a;
			}
			#elif (CUTOUT)
			{
				color = 0.0;
			}
			#endif
		}
	
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
			Name "Blend-DropShadow"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragDropShadow
			ENDCG
		}
	}
}
