Shader "Hidden/ChocDino/UIFX/DistanceMap"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _InsideTex ("Inside Texture", 2D) = "white" {}
		_StepSize ("Step Size", Vector) = (1.0, 1.0, 0.0, 0.0)
		_AlphaRange("Alpha Range", Vector) = (0.0, 1.0, 0.0, 0.0)
		_DownSample ("Down Sample", Int) = 1
		[KeywordEnum(Square,Diamond,Circle)] Dist("Dist",int) = 0
	}

	CGINCLUDE

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

	uniform float4 _MainTex_TexelSize;
	uniform float2 _StepSize;
	uniform float2 _AlphaRange;
	uniform float _DownSample;

	UNITY_DECLARE_TEX2D(_MainTex);
	UNITY_DECLARE_TEX2D(_InsideTex);
	
	v2f vert (appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;
		return o;
	}

	// Manhattan distance
	float distanceDiamond(float2 a, float2 b)
	{
		return abs(a.x - b.x) + abs(a.y - b.y);
	}
	// Chebyshev distance
	float distanceSquare(float2 a, float2 b)
	{
		return max(abs(a.x - b.x), abs(a.y - b.y));
	}

	float computeDistance(float2 p0, float2 p1)
	{
		#if DIST_CIRCLE
		return distance(p0, p1);
		#elif DIST_DIAMOND
		return distanceDiamond(p0, p1);
		#elif DIST_SQUARE
		return distanceSquare(p0, p1);
		#endif
	}

	float comparitiveDistance(float2 p0, float2 p1, float2 pixelScale)
	{
		#if DIST_CIRCLE
		// Uses squared distance (faster)
		float2 v = (p0 - p1) * pixelScale;
		return dot(v, v);
		#elif DIST_DIAMOND
		return distanceDiamond(p0 * pixelScale, p1 * pixelScale);
		#elif DIST_SQUARE
		return distanceSquare(p0 * pixelScale, p1 * pixelScale);
		#endif
	}

	#define NULL_PIXEL -1.0

	float2 fragAlphaToUVCommon(v2f i, bool invert)
	{
		float2 offset = _MainTex_TexelSize.xy;

		float2 result = i.uv;

		float alpha = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv, 0).a;
		alpha = saturate((alpha - _AlphaRange.x) / _AlphaRange.y);

		if (!invert)
		{
			if (alpha > 0.99)
			{
				return result;
			}
			// Changed this from 0.01 to 0.016 to filter out some very tiny values introduced in some textues when using HIGH compression setting
			if (alpha < 0.016)
			{
				return NULL_PIXEL;
			}
		}
		else
		{
			alpha = 1.0 - alpha;

			if (alpha > 0.99)
			{
				return result;
			}
			if (alpha < 0.01)
			{
				return NULL_PIXEL;
			}
		}

		// For intermediate alpha values use sobel filter to estimate the fractional offset of the subpixel
		// Credit for this idea to Ben Golus "The Quest for Very Wide Outlines".
		float c00 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv - offset.xy, 0).a;
		float c10 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + float2(0.0, -offset.y), 0).a;
		float c20 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + float2(offset.x, -offset.y), 0).a;

		float c01 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + float2(-offset.x, 0.0), 0).a;
		float c21 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + float2(offset.x, 0.0), 0).a;

		float c02 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + float2(-offset.x, offset.y), 0).a;
		float c12 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + float2(0.0, offset.y), 0).a;
		float c22 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + float2(offset.x, offset.y), 0).a;

		// Sobel gradient to estimate edge direction
		float sobelX = c00 + c01 * 2.0 + c02 - c20 - c21 * 2.0 - c22;
		float sobelY = c00 + c10 * 2.0 + c20 - c02 - c12 * 2.0 - c22;
		float2 dir = invert ? float2(sobelX, sobelY) : -float2(sobelX, sobelY);

		// If dir length is small, this is either a sub pixel dot or line
		// no way to estimate sub pixel edge, so output position
		if (abs(dir.x) <= 0.005 && abs(dir.y) <= 0.005)
		{
			return result;
		}

		// normalize direction
		dir = normalize(dir);

		// sub pixel offset
		offset *= dir * (1.0 - alpha);

		result = (i.uv + offset);

		return result;
	}

	#if !defined(SHADER_API_GLES3) && !defined(SHADER_API_WEBGL)
	float2 fragAlphaToUVGather(v2f i, bool invert)
	{
		float2 offset = _MainTex_TexelSize.xy;

		float2 result = i.uv;

		float alpha = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv, 0).a;
		alpha = saturate((alpha - _AlphaRange.x) / _AlphaRange.y);

		if (!invert)
		{
			if (alpha > 0.99)
			{
				return result;
			}
			// Changed this from 0.01 to 0.016 to filter out some very tiny values introduced in some textues when using HIGH compression setting
			if (alpha < 0.016)
			{
				return NULL_PIXEL;
			}
		}
		else
		{
			alpha = 1.0 - alpha;

			if (alpha > 0.99)
			{
				return result;
			}
			if (alpha < 0.01)
			{
				return NULL_PIXEL;
			}
		}

		// For intermediate alpha values use sobel filter to estimate the fractional offset of the subpixel
		// Credit for this idea to Ben Golus "The Quest for Very Wide Outlines".

		// Gather Alpha returns a float4 containing 2x2 4 neighboring texels at once.
		// Hardware Layout: x = Top-Left, y = Top-Right, z = Bottom-Right, w = Bottom-Left.
		// We sample at 4 strategic half-pixel diagonal locations to cover the 3x3 grid.
		// This is mostly a win on mobile platforms.

		float4 tl = _MainTex.GatherAlpha(sampler_MainTex, i.uv + float2(-0.5, -0.5) * offset);
		float c00 = tl.w;   // (-1, -1)
		float c10 = tl.z;   // ( 0, -1)
		float c01 = tl.x;   // (-1,  0)

		float4 tr = _MainTex.GatherAlpha(sampler_MainTex, i.uv + float2(0.5, -0.5) * offset);
		float c20 = tr.z;   // (+1, -1)
		float c21 = tr.y;   // (+1,  0)

		float4 bl = _MainTex.GatherAlpha(sampler_MainTex, i.uv + float2(-0.5, 0.5) * offset);
		float c02 = bl.x;   // (-1, +1)
		float c12 = bl.y;   // ( 0, +1)

		float4 br = _MainTex.GatherAlpha(sampler_MainTex, i.uv + float2(0.5, 0.5) * offset);
		float c22 = br.y;   // (+1, +1)

		// Sobel gradient to estimate edge direction
		float sobelX = c00 + c01 * 2.0 + c02 - c20 - c21 * 2.0 - c22;
		float sobelY = c00 + c10 * 2.0 + c20 - c02 - c12 * 2.0 - c22;
		float2 dir = invert ? float2(sobelX, sobelY) : -float2(sobelX, sobelY);

		// If dir length is small, this is either a sub pixel dot or line
		// no way to estimate sub pixel edge, so output position
		if (abs(dir.x) <= 0.005 && abs(dir.y) <= 0.005)
		{
			return result;
		}

		// normalize direction
		dir = normalize(dir);

		// sub pixel offset
		offset *= dir * (1.0 - alpha);

		result = (i.uv + offset);

		return result;
	}
	#endif

	float2 fragAlphaToUV(v2f i) : SV_Target
	{
		return fragAlphaToUVCommon(i, false);
	}

	float2 fragAlphaToUV_Inverted(v2f i) : SV_Target
	{
		return fragAlphaToUVCommon(i, true);
	}

	float2 fragAlphaToUV_Gather(v2f i) : SV_Target
	{
		#if !defined(SHADER_API_GLES3) && !defined(SHADER_API_WEBGL)
		return fragAlphaToUVGather(i, false);
		#else
		return fragAlphaToUVCommon(i, false);
		#endif
	}

	float2 fragAlphaToUV_InvertedGather(v2f i) : SV_Target
	{
		#if !defined(SHADER_API_GLES3) && !defined(SHADER_API_WEBGL)
		return fragAlphaToUVGather(i, true);
		#else
		return fragAlphaToUVCommon(i, true);
		#endif
	}

	float2 fragJumpFlood(v2f i) : SV_Target
	{
		float2 pixelScale = _MainTex_TexelSize.zw;
		float2 offset = _MainTex_TexelSize.xy * _StepSize;
		float2 closest = NULL_PIXEL;
		// NOTE: this value should be big enough, as using 1.#INF was causing problems with some console compilers
		#if SHADER_API_GLES || SHADER_API_MOBILE || SHADER_API_SWITCH 
		float minDist = 32768.0;
		#else
		float minDist = 16777216.0;
		#endif

		float2 p0 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv, 0).xy;
		if (p0.x > NULL_PIXEL)
		{
			float2 v3 = (p0 - i.uv) * pixelScale;
			float dd = dot(v3, v3);
			if (dd < 2)//(abs(p0.x - i.uv.x) < _MainTex_TexelSize.x*1.5) && (abs(p0.y - i.uv.y) < _MainTex_TexelSize.y*1.5))
			{
				return p0;
			}

			closest = p0;
			#if DIST_CIRCLE
			minDist = dd;
			#else
			minDist = comparitiveDistance(p0, i.uv, pixelScale);
			#endif
		}

		UNITY_UNROLL
		for (int y = -1; y <= 1; y++)
		{
			UNITY_UNROLL
			for (int x = -1; x <= 1; x++)
			{
				if (x == 0 && y == 0) continue;

				float2 p = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + offset * float2(x, y), 0).xy;
				if (p.x > NULL_PIXEL)
				{
					float sd = comparitiveDistance(p, i.uv, pixelScale);
					if (sd < minDist)
					{
						minDist = sd;
						closest = p;
					}
				}
			}
		}
		return closest;
	}
	
	float2 fragJumpFloodSingleAxis(v2f i) : SV_Target
	{
		float2 pixelScale =  _MainTex_TexelSize.zw;
		float2 offset = _MainTex_TexelSize.xy * _StepSize;
		float2 closest = NULL_PIXEL;
		// NOTE: this value should be big enough, as using 1.#INF was causing problems with some console compilers
		#if SHADER_API_GLES || SHADER_API_MOBILE || SHADER_API_SWITCH 
		float minDist = 32768.0;
		#else
		float minDist = 16777216.0;
		#endif

		float2 p0 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv, 0).xy;
		if (p0.x > NULL_PIXEL)
		{
			float2 v3 = (p0 - i.uv) * pixelScale;
			float dd = dot(v3, v3);
			if (dd < 2)//(abs(p0.x - i.uv.x) < _MainTex_TexelSize.x*1.5) && (abs(p0.y - i.uv.y) < _MainTex_TexelSize.y*1.5))
			{
				return p0;
			}
			
			closest = p0;
			#if DIST_CIRCLE
			minDist = dd;
			#else
			minDist = comparitiveDistance(p0, i.uv, pixelScale);
			#endif
		}
		
		UNITY_UNROLL
		for (int x = -1; x <= 1; x++)
		{
			if (x == 0) continue;
			float2 p = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv + offset * x, 0).xy;
			if (p.x > NULL_PIXEL)
			{
				float sd = comparitiveDistance(p, i.uv, pixelScale);
				if (sd < minDist)
				{
					minDist = sd;
					closest = p;
				}
			}
		}
		return closest;
	}

	// NOTE: In GLES2.0 if you return float it gives an error "Type mismatch, cannot convert from 'float' to 'vec4'", so just return float4
	float4 fragResolveDistance(v2f i) : SV_Target
	{
		float2 pixelScale = _MainTex_TexelSize.zw;

		float2 p0 = i.uv * pixelScale;
		float2 p1 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv, 0).xy * pixelScale;

		float d = 0.0;
		//if (p1.x > NULL_PIXEL)
		{
			d = computeDistance(p0, p1);

			// Subtract to eliminate noise where distances are almost equal
			// TODO: ideally shouldn't even write these pixels, just zero them
			//d = max(0, d - 2.0);
		}

		return d * _DownSample;
	}

	// NOTE: In GLES2.0 if you return float it gives an error "Type mismatch, cannot convert from 'float' to 'vec4'", so just return float4
	float4 fragResolveDistanceIOnOutMax(v2f i) : SV_Target
	{
		float2 pixelScale = _MainTex_TexelSize.zw;

		float2 p0 = i.uv * pixelScale;
		float2 p1 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv, 0).xy * pixelScale;
		float dOut = computeDistance(p0, p1);

		p1 = UNITY_SAMPLE_TEX2D_LOD(_InsideTex, i.uv, 0).xy * pixelScale;
		float dIn = computeDistance(p0, p1);

		float d = max(dOut, dIn);

		return (d * _DownSample);
	}

	// NOTE: In GLES2.0 if you return float it gives an error "Type mismatch, cannot convert from 'float' to 'vec4'", so just return float4
	float4 fragResolveDistanceSDF(v2f i) : SV_Target
	{
		float2 pixelScale = _MainTex_TexelSize.zw;

		float2 p0 = i.uv * pixelScale;
		float2 p1 = UNITY_SAMPLE_TEX2D_LOD(_MainTex, i.uv, 0).xy * pixelScale;
		float dOut = computeDistance(p0, p1);

		p1 = UNITY_SAMPLE_TEX2D_LOD(_InsideTex, i.uv, 0).xy * pixelScale;
		float dIn = computeDistance(p0, p1);

		float d = dOut - dIn;

		return (d * _DownSample);
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
		}

		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "AlphaToUV"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragAlphaToUV
			ENDCG
		}
		Pass
		{
			Name "InvAlphaToUV"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragAlphaToUV_Inverted
			ENDCG
		}
		Pass
		{
			Name "JumpFlood"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragJumpFlood
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "JumpFloodSingleAxis"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragJumpFloodSingleAxis
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "ResolveDistance"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragResolveDistance
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "ResolveDistanceInOutMax"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragResolveDistanceIOnOutMax
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "ResolveDistanceSDF"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragResolveDistanceSDF
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "AlphaToUV-Gather"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragAlphaToUV_Gather
			#pragma target 4.5
			ENDCG
		}
		Pass
		{
			Name "InvAlphaToUV-Gather"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragAlphaToUV_InvertedGather
			#pragma target 4.5
			ENDCG
		}
	}

	// This subshader doesn't support GATHER passes (for WEBGL)
	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "AlphaToUV"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragAlphaToUV
			ENDCG
		}
		Pass
		{
			Name "InvAlphaToUV"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragAlphaToUV_Inverted
			ENDCG
		}
		Pass
		{
			Name "JumpFlood"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragJumpFlood
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "JumpFloodSingleAxis"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragJumpFloodSingleAxis
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "ResolveDistance"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragResolveDistance
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "ResolveDistanceInOutMax"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragResolveDistanceIOnOutMax
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
		Pass
		{
			Name "ResolveDistanceSDF"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragResolveDistanceSDF
			#pragma multi_compile_local_fragment DIST_SQUARE DIST_DIAMOND DIST_CIRCLE
			ENDCG
		}
	}
}