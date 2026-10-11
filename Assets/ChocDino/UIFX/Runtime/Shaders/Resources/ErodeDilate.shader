Shader "Hidden/ChocDino/UIFX/ErodeDilate"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_ErodeRadius ("Erode Radius", float) = 0.0
		_DilateRadius ("Dilate Radius", float) = 0.0
		[KeywordEnum(Square,Diamond,Circle)] Dist("Dist",int) = 0
	}

	CGINCLUDE

	#include "UnityCG.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};	

	sampler2D _MainTex;
	float4 _MainTex_ST;
	float4 _MainTex_TexelSize;

	float _ErodeRadius;
	float _DilateRadius;

	v2f vert (appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.uv, _MainTex);
		return o;
	}

	// Manhattan distance
	float distanceDiamond(float2 a)
	{
		return abs(a.x) + abs(a.y);
	}
	// Chebyshev distance
	float distanceSquare(float2 a)
	{
		return max(abs(a.x), abs(a.y));
	}
	
	float4 Erode(float2 uv, float2 texelSize)
	{	
		float4 result = 1.0;

		float2 step = texelSize;
		int m = ceil(_ErodeRadius);
		float2 offset = step;
		for (int i = -m; i <= m; i++)
		for (int j = -m; j <= m; j++)
		{
			offset = texelSize * float2(i, j);
			float4 color = tex2D(_MainTex, uv + offset);

			float radius = 0.0;
			#if DIST_CIRCLE
			radius = length(float2(i, j));
			#elif DIST_DIAMOND
			radius = distanceDiamond(float2(i, j));
			#elif DIST_SQUARE
			radius = distanceSquare(float2(i, j));
			#endif

			float remainder = saturate(radius - _ErodeRadius);
			result = min(result, lerp(color, result, remainder));
		}

		return result;
	}

	float4 Dilate(float2 uv, float2 texelSize)
	{	
		float4 result = 0.0;

		float2 step = texelSize;
		int m = ceil(_DilateRadius);
		float2 offset = step;
		for (int i = -m; i <= m; i++)
		for (int j = -m; j <= m; j++)
		{
			offset = texelSize * float2(i, j);
			float4 color = tex2D(_MainTex, uv + offset);

			float radius = 0.0;
			#if DIST_CIRCLE
			radius = length(float2(i, j));
			#elif DIST_DIAMOND
			radius = distanceDiamond(float2(i, j));
			#elif DIST_SQUARE
			radius = distanceSquare(float2(i, j));
			#endif

			float remainder = saturate(radius - _DilateRadius);
			result = max(result, lerp(color, result, remainder));
		}

		return result;
	}

	float DilateAlpha(float2 uv, float2 texelSize)
	{	
		float result = 0.0;

		float2 step = texelSize;
		int m = ceil(_DilateRadius);
		float2 offset = step;
		for (int i = -m; i <= m; i++)
		for (int j = -m; j <= m; j++)
		{
			offset = texelSize * float2(i, j);
			float a = tex2D(_MainTex, uv + offset).r;

			float radius = 0.0;
			#if DIST_CIRCLE
			radius = length(float2(i, j));
			#elif DIST_DIAMOND
			radius = distanceDiamond(float2(i, j));
			#elif DIST_SQUARE
			radius = distanceSquare(float2(i, j));
			#endif

			float remainder = saturate(radius - _DilateRadius);
			result = max(result, lerp(a, result, remainder));
		}

		return result;
	}

	float ErodeAlpha(float2 uv, float2 texelSize)
	{	
		float result = 1.0;

		float2 step = texelSize;
		int m = ceil(_ErodeRadius);
		float2 offset = step;
		for (int i = -m; i <= m; i++)
		for (int j = -m; j <= m; j++)
		{
			offset = texelSize * float2(i, j);
			float a = tex2D(_MainTex, uv + offset).r;

			float radius = 0.0;
			#if DIST_CIRCLE
			radius = length(float2(i, j));
			#elif DIST_DIAMOND
			radius = distanceDiamond(float2(i, j));
			#elif DIST_SQUARE
			radius = distanceSquare(float2(i, j));
			#endif

			float remainder = saturate(radius - _ErodeRadius);
			result = min(result, lerp(a, result, remainder));
		}

		return result;
	}

	float ErodeDilateAlpha(float2 uv, float2 texelSize)
	{	
		float resultErode = 1.0;
		float resultDilate = 0.0;

		float2 step = texelSize;

		int m = max(ceil(_ErodeRadius), ceil(_DilateRadius));
		float2 offset = step;
		for (int i = -m; i <= m; i++)
		for (int j = -m; j <= m; j++)
		{
			offset = texelSize * float2(i, j);
			float a = tex2D(_MainTex, uv + offset).r;

			float radius = 0.0;
			#if DIST_CIRCLE
			radius = length(float2(i, j));
			#elif DIST_DIAMOND
			radius = distanceDiamond(float2(i, j));
			#elif DIST_SQUARE
			radius = distanceSquare(float2(i, j));
			#endif

			float remainderErode = saturate(radius - _ErodeRadius);
			resultErode = min(resultErode, lerp(a, resultErode, remainderErode));

			float remainderDilate = saturate(radius - _DilateRadius);
			resultDilate = max(resultDilate, lerp(a, resultDilate, remainderDilate));
		}

		return abs(resultErode - resultDilate);
	}

	float4 fragErode(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		return Erode(i.uv, _MainTex_TexelSize);
	}

	float4 fragDilate(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		return Dilate(i.uv, _MainTex_TexelSize);
	}

	float4 fragDilateAlpha(v2f i) : SV_Target
	{
		return DilateAlpha(i.uv, _MainTex_TexelSize);
	}

	float4 fragErodeAlpha(v2f i) : SV_Target
	{
		return ErodeAlpha(i.uv, _MainTex_TexelSize);
	}

	float4 fragErodeDilateAlpha(v2f i) : SV_Target
	{
		return ErodeDilateAlpha(i.uv, _MainTex_TexelSize);
	}

	float4 fragCopyAlpha(v2f i) : SV_Target
	{
		return tex2D(_MainTex, i.uv).a;
	}

	float4 fragNull(v2f i) : SV_Target
	{
		return 0.0;
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

		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "ErodeAlpha"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragErodeAlpha
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}

		Pass
		{
			Name "DilateAlpha"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragDilateAlpha
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}

		Pass
		{
			Name "ErodeDilateAlpha"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragErodeDilateAlpha
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}

		Pass
		{
			Name "Erode"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragErode
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}

		Pass
		{
			Name "Dilate"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragDilate
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}

		Pass
		{
			Name "CopyAlpha"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragCopyAlpha
			ENDCG
		}

		Pass
		{
			Name "Null"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragNull
			ENDCG
		}
	}
}