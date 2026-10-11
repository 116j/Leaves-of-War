Shader "Hidden/ChocDino/UIFX/Blend-Outline"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}
		[PerRendererData] _FillTex ("Fill Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_SourceAlpha ("Source Alpha", Float) = 1
		_OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)
		_Size ("Size", Vector) = (1, 1, 1, 1)

		_GradientLinearStartLine("Gradient Linear Start Line", Vector) = (0, 0, 1, 1)
		_GradientLinearParams("Gradient Linear Params", Vector) = (1, 1, 0, 0)
		
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local_fragment DIR_BOTH DIR_INSIDE DIR_OUTSIDE
	#pragma multi_compile_local_fragment __ DISTANCEMAP
	#pragma multi_compile_local_fragment __ USE_TEXTURE
	//#pragma multi_compile_local GRADIENT_MIX_SMOOTH GRADIENT_MIX_LINEAR GRADIENT_MIX_STEP
	//#pragma multi_compile_local GRADIENT_COLORSPACE_SRGB GRADIENT_COLORSPACE_LINEAR GRADIENT_COLORSPACE_PERCEPTUAL
		
	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"
	#include "Common/GradientUtils.cginc"

	uniform float _SourceAlpha;
	uniform float4 _OutlineColor;
	//uniform sampler2D _FillTex;
	//uniform float4 _FillTex_ST;
	uniform float2 _Size;

	#if USE_TEXTURE
	uniform sampler2D _FillTex;
	uniform float4 _GradientLinearStartLine;	//PositionxXY, DirectionXY
	uniform float4 _GradientLinearParams;		//uvRectRatioXY, 0, 0
	#endif

	struct v2fOutline
	{
		float4 vertex : SV_POSITION;
		float4 uv : TEXCOORD0;
		float4 color : TEXCOORD1;
		#ifdef UNITY_UI_CLIP_RECT
		float4 mask : TEXCOORD2;
		#endif

		UNITY_VERTEX_OUTPUT_STEREO // VR SUPPORT
	};

	v2fOutline vertOutline (appdata v)
	{
		v2fOutline o;

		UNITY_SETUP_INSTANCE_ID(v); // VR SUPPORT
		UNITY_INITIALIZE_OUTPUT(v2fOutline, o); // VR SUPPORT
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // VR SUPPORT
	
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv.xy = TRANSFORM_TEX(v.uv, _ResultTex);
		o.uv.zw = 0;
		//o.uv.zw = TRANSFORM_TEX(v.uv, _FillTex);
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

	float4 fragOutline(v2fOutline i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		float4 source = tex2D(_SourceTex, i.uv.xy);
		
		// Note: Need to do this explicit swizzling for console compilers
		float zero = 0.0;
		float4 color = lerp(zero.xxxx, source, _SourceAlpha.xxxx);

		#if USE_TEXTURE

		float2 fillUV = 0.0;
		{
			// Scale UV by aspect ratio
			float2 uv = i.uv.xy * float2(_GradientLinearParams.y, 1.0);

			float2 startPoint = _GradientLinearStartLine.xy;
			float2 startDir = _GradientLinearStartLine.zw;
			float dir = dot(uv - startPoint, startDir);
			float2 closestPoint = startPoint + startDir * dir;
			fillUV.x = distance(closestPoint, uv) / _GradientLinearParams.x;
		}
		
		float4 outlineColor = ToPremultiplied(tex2D(_FillTex, fillUV)) * _OutlineColor.a;

		#else

		float4 outlineColor = ToPremultiplied(_OutlineColor);

		//outlineColor += ToPremultiplied(tex2D(_FillTex, i.uv.zw));

		#endif

		float outlineMask = 0.0;

		#ifdef DISTANCEMAP
		{
			float distance = tex2D(_ResultTex, i.uv.xy).x;

			//float mask = saturate(step(distance, _Size.x) + (1.0-smoothstep(_Size.x, _Size.y, distance)));
			//float mask = saturate(distance - _Size*0.2) * saturate(_Size.x - distance + 1.0);

			float fillEdgeDistance = _Size.x - (distance-1.0);
			float aaf = saturate(fillEdgeDistance + 0.5) * max(0.5, _Size.y);
			outlineMask = smoothstep(0.0, aaf, fillEdgeDistance);
			
			//outlineColor = EvalulateGradient(smoothstep(0, _Size.y, distance));
			//outlineColor = EvalulateGradient(i.uv.y);
			//outlineColor = (smoothstep(0, _Size.y, distance));
		}
		#else
		{
			outlineMask = tex2D(_ResultTex, i.uv.xy).x;

			#if (DIR_INSIDE)
			{
				outlineMask = saturate(1.0 - outlineMask);
			}
			#endif
		}
		#endif

		

		#if (DIR_INSIDE)
		{
			outlineColor *= outlineMask;
			color = AlphaComp_Over(outlineColor, color) * source.a;
		}
		#elif (DIR_OUTSIDE)
		{
			outlineColor *= outlineMask * (1.0-source.a);
			color = (color + outlineColor);
		}
		#elif (DIR_BOTH)
		{
			outlineColor *= outlineMask;
			color = AlphaComp_Over(outlineColor, color);
		}
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
			Name "Blend-Outline"
			CGPROGRAM
			#pragma vertex vertOutline
			#pragma fragment fragOutline
			ENDCG
		}
	}
}
