uniform int _GradientColorCount;
uniform int _GradientAlphaCount;
uniform float4 _GradientColors[8];
uniform float4 _GradientAlphas[8];

uniform float4 _GradientTransform; // scale, scalePivot, offset, wrapMode
uniform float _GradientDither;

#if GRADIENT_SHAPE_LINEAR
uniform float4 _GradientLinearStartLine;
uniform float4 _GradientLinearParams;
#endif

#if GRADIENT_SHAPE_RADIAL || GRADIENT_SHAPE_CONIC
uniform float4 _GradientRadial; // centerX, centerY, radius, 0.0
#endif

float RadialGradientDistance(float2 uv, float2 centerPos, float radius)
{
	return length(uv - centerPos) / radius;
}

/*
float RadialFocalGradientDistance(float2 uv, float2 centerPos, float radius, float2 focalPos, float focalRadius)
{
	//vec2 center = vec2(cx, uv.y - cy);
	//vec2 focal = vec2(fx, uv.y - fy);

	float x = focalPos.x - uv.x;
	float y = focalPos.y - uv.y;
	float dx = focalPos.x - centerPos.x;
	float dy = focalPos.y - centerPos.y;
	float r0 = focalRadius;
	float dr = radius - focalRadius;
	float a = dx * dx + dy * dy - dr * dr;
	float b = -2.0 * (y * dy + x * dx + r0 * dr);
	float c = x*x + y*y - r0*r0;
	float t = 0.5 * (1.0/a) * (-b + sqrt(b*b - 4.0*a*c));
	t = 1.0 - t;
	return t;
}*/

// Gradient noise from Jorge Jimenez's presentation:
// http://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare
float gradientNoise(in float2 uv)
{
	static const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
	return frac(magic.z * frac(dot(uv, magic.xy)));
}

// t must be [0..1]
// A good default value for scaleCenter is 0.5
inline float WrapClamp(float t, float scale, float scaleCenter, float offset)
{
	t -= scaleCenter;
	t *= scale;
	t += scaleCenter;
	t += offset;
	return t;
}

// t must be [0..1]
// A good default value for scaleCenter is 0.5
float WrapRepeat(float t, float scale, float scaleCenter, float offset)
{
	t = WrapClamp(t, scale, scaleCenter, offset);
	t = saturate(t - floor(t / 1.0) * 1.0);
	return t;
}

// t must be [0..1]
// A good default value for scaleCenter is 0.5
float WrapMirror(float t, float scale, float scaleCenter, float offset)
{
	t = WrapClamp(t, scale, scaleCenter, offset);
	t = clamp(t - floor(t / 2.0) * 2.0, 0.0, 2.0);
	t = 1.0 - abs(t - 1.0);
	return t;
}

// t must be [0..1]
// A good default value for scaleCenter is 0.5
float Wrap(float t, float scale, float scaleCenter, float offset, float mode)
{
	t -= scaleCenter;
	t *= scale;
	t += scaleCenter;
	t += offset;

	if (mode >= 1.0)
	{
		if (mode >= 2.0)
		{
			// Mirror
			t = clamp(t - floor(t / 2.0) * 2.0, 0.0, 2.0);
			t = 1.0 - abs(t - 1.0);
		}
		else
		{
			// Repeat
			t = saturate(t - floor(t));
		}	
	}

	return t;
}

float4 EvalGradient_Step(int colorCount, float4 colors[8], float t)
{
	float4 result = colors[0];
	for (int i = 1; i < colorCount; i++)
	{
		float t0 = colors[i-1].w;
		float t1 = colors[i+0].w;

		float tt = smoothstep(t0, t1, step(t0, t));
	  
		result = lerp(result, colors[i], tt);
	}
	return result;
}

// t must be [0..1]
// colors array contains (RGB, Stop)
// alphas array contains (A, 0, 0, Stop)
float4 Gradient_EvalStep(int colorCount, float4 colors[8], int alphaCount, float4 alphas[8], float t)
{
	float4 result = EvalGradient_Step(colorCount, colors, t);
	float4 resulta = EvalGradient_Step(alphaCount, alphas, t);
	return float4(result.rgb, resulta.x);
}

float4 EvalGradient_StepAA(int colorCount, float4 colors[8], float t, float afwidth)
{
	float4 result = colors[0];
	for (int i = 1; i < colorCount; i++)
	{
		float t0 = colors[i-1].w;
		float t1 = colors[i+0].w;

		float tt = smoothstep(t0, t1, smoothstep(t0 - afwidth, t0 + afwidth, t));
	  
		result = lerp(result, colors[i], tt);
	}
	return result;
}

// t must be [0..1]
// colors array contains (RGB, Stop)
// alphas array contains (A, 0, 0, Stop)
float4 Gradient_EvalStepAA(int colorCount, float4 colors[8], int alphaCount, float4 alphas[8], float t)
{
	float afwidth = length(float2(ddx(t), ddy(t))) * 2.0;

	float4 result = EvalGradient_StepAA(colorCount, colors, afwidth, t);
	float4 resulta = EvalGradient_StepAA(alphaCount, alphas, afwidth, t);
	return float4(result.rgb, resulta.x);
}

float4 EvalGradient_Linear(int colorCount, float4 colors[8], float t)
{
	float4 result = colors[0];
	for (int i = 1; i < colorCount; i++)
	{
		float t0 = colors[i-1].w;
		float t1 = colors[i+0].w;

		float tt = saturate((t - t0)/(t1 - t0));
	  
		result = lerp(result, colors[i], tt);
	}
	return result;
}

// t must be [0..1]
// colors array contains (RGB, Stop)
// alphas array contains (A, 0, 0, Stop)
float4 Gradient_EvalLinear(int colorCount, float4 colors[8], int alphaCount, float4 alphas[8], float t)
{
	float4 result = EvalGradient_Linear(colorCount, colors, t);
	float4 resulta = EvalGradient_Linear(alphaCount, alphas, t);
	return float4(result.rgb, resulta.x);
}

float4 EvalGradient_Smooth(int colorCount, float4 colors[8], float t)
{
	float4 result = colors[0];
	for (int i = 1; i < colorCount; i++)
	{
		float t0 = colors[i-1].w;
		float t1 = colors[i+0].w;

		float tt = smoothstep(t0, t1, t);
	  
		result = lerp(result, colors[i], tt);
	}
	return result;
}

// t must be [0..1]
// colors array contains (RGB, Stop)
// alphas array contains (A, 0, 0, Stop)
float4 Gradient_EvalSmooth(int colorCount, float4 colors[8], int alphaCount, float4 alphas[8], float t)
{
	float4 result = EvalGradient_Smooth(colorCount, colors, t);
	float4 resulta = EvalGradient_Smooth(alphaCount, alphas, t);
	return float4(result.rgb, resulta.x);
}

float4 EvalulateGradient(float t)
{
	//float dd = _GradientDither * gradientNoise(i.uv*_ResultTex_TexelSize.zw) - (_GradientDither * 0.5);

	t = Wrap(t, _GradientTransform.x, _GradientTransform.y, _GradientTransform.z, _GradientTransform.w);
	
	#ifdef GRADIENT_MIX_LINEAR
	float4 fill = Gradient_EvalLinear(_GradientColorCount, _GradientColors, _GradientAlphaCount, _GradientAlphas, t);
	#elif GRADIENT_MIX_STEP
	float4 fill = Gradient_EvalStepAA(_GradientColorCount, _GradientColors, _GradientAlphaCount, _GradientAlphas, t);
	#else
	float4 fill = Gradient_EvalSmooth(_GradientColorCount, _GradientColors, _GradientAlphaCount, _GradientAlphas, t);
	#endif

	//fill += (dither/255.0) * gradientNoise(i.uv*_ResultTex_TexelSize.zw) - ((dither * 0.5)/255.0);
	//fill += ((4.0/255.0) * saturate(gold_noise(i.uv*_ResultTex_TexelSize.zw, _Time.x)) - (2.0/255.0));
	//fill = saturate(fill);

	#ifdef GRADIENT_COLORSPACE_SRGB
	fill.rgb = GammaToLinearSpace(fill.rgb);
	#elif GRADIENT_COLORSPACE_PERCEPTUAL
	fill.rgb = OkLabToLinear(fill.rgb);
	#elif GRADIENT_COLORSPACE_LINEAR
	#else
	#endif

	return fill;
}
