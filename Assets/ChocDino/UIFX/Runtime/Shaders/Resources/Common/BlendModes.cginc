float4
BlendMode_Normal(float4 base, float4 blend)
{
	return blend;
}

float4
BlendMode_Darken(float4 base, float4 blend)
{
	return min(base, blend);
}

float4
BlendMode_Multiply(float4 base, float4 blend)
{
	return base * blend;
}

float4
BlendMode_ColorBurn(float4 base, float4 blend)
{
	return 1.0 - (1.0 - base) / blend;
}

float4
BlendMode_LinearBurn(float4 base, float4 blend)
{
	return max(0.0, base + blend - 1.0);
}

float4
BlendMode_Lighten(float4 base, float4 blend)
{
	return max(base, blend);
}

float4
BlendMode_Screen(float4 base, float4 blend)
{
	return 1.0 - (1.0 - blend) * (1.0 - base);
}

float4
BlendMode_ColorDodge(float4 base, float4 blend)
{
	return base / (1.0 - blend);
}

float4
BlendMode_LinearDodge(float4 base, float4 blend)
{
	return base + blend;
}

float4
BlendMode_Overlay(float4 base, float4 blend)
{
	float4 result1 = 1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
	float4 result2 = 2.0 * base * blend;
	float4 zeroOrOne = step(base, 0.5);
	return result2 * zeroOrOne + (1 - zeroOrOne) * result1;
}

float4
BlendMode_LinearLight(float4 base, float4 blend)
{
	return (blend < 0.5) ? max(base + (2.0 * blend) - 1.0, 0.0) : min(base + 2.0 * (blend - 0.5), 1.0);
}

float4
BlendMode_HardLight(float4 base, float4 blend)
{
	float4 result1 = 1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
	float4 result2 = 2.0 * base * blend;
	float4 zeroOrOne = step(blend, 0.5);
	return result2 * zeroOrOne + (1 - zeroOrOne) * result1;
}

float4
BlendMode_SoftLight(float4 base, float4 blend)
{
	float4 result1 = 2.0 * base * blend + base * base * (1.0 - 2.0 * blend);
	float4 result2 = sqrt(base) * (2.0 * blend - 1.0) + 2.0 * base * (1.0 - blend);
	float4 zeroOrOne = step(0.5, blend);
	return result2 * zeroOrOne + (1 - zeroOrOne) * result1;
}

float4
BlendMode_PinLight(float4 base, float4 blend)
{
	float4 check = step(0.5, blend);
	float4 result1 = check * max(2.0 * (base - 0.5), blend);
	return result1 + (1.0 - check) * min(2.0 * base, blend);
}

float4
BlendMode_HardMix(float4 base, float4 blend)
{
	return step(1.0 - base, blend);
}

float4
BlendMode_VividLight(float4 base, float4 blend)
{
	float4 result1 = 1.0 - (1.0 - blend) / (2.0 * base);
	float4 result2 = blend / (2.0 * (1.0 - base));
	float4 zeroOrOne = step(0.5, base);
	return result2 * zeroOrOne + (1 - zeroOrOne) * result1;
}

float4
BlendMode_Difference(float4 base, float4 blend)
{
	return abs(blend - base);
}

float4
BlendMode_Exclusion(float4 base, float4 blend)
{
	return blend + base - (2.0 * blend * base);
}

float4
BlendMode_Subtract(float4 base, float4 blend)
{
	return max(0.0, base - blend);
}

float4
BlendMode_Divide(float4 base, float4 blend)
{
	return blend > 0.0 ? min(1.0, base / blend) : 1.0;
}


#define BLENDMODE_REPLACE 0
#define BLENDMODE_BLEND 1
#define BLENDMODE_OVER 2
#define BLENDMODE_UNDER 3
#define BLENDMODE_MASK 4
#define BLENDMODE_CLEAR 5

#define BLENDMODE_DARKEN 10
#define BLENDMODE_MULTIPLY 11
#define BLENDMODE_COLOR_BURN 12
#define BLENDMODE_LINEAR_BURN 13

#define BLENDMODE_LIGHTEN 20
#define BLENDMODE_SCREEN 21
#define BLENDMODE_COLOR_DODGE 22
#define BLENDMODE_LINEAR_DODGE 23

#define BLENDMODE_OVERLAY 30
#define BLENDMODE_SOFT_LIGHT 31
#define BLENDMODE_HARD_LIGHT 32
#define BLENDMODE_VIVID_LIGHT 33
#define BLENDMODE_LINEAR_LIGHT 34
#define BLENDMODE_PIN_LIGHT 35
#define BLENDMODE_HARD_MIX 36

#define BLENDMODE_DIFFERENCE 50
#define BLENDMODE_EXCLUSION 51
#define BLENDMODE_SUBTRACT 52
#define BLENDMODE_DIVIDE 53

#define BLENDMODE_REPLACE_ALPHA 60
#define BLENDMODE_REPLACE_BACKGROUND 61

float4
BlendMode(int blendMode, float4 base, float4 blend)
{
	float4 result = base;
	switch (blendMode)
	{
		// Normal modes
//		case BLENDMODE_NORMAL:
//		result = BlendMode_Normal(base, blend);
//		break;

		// Darken modes
		case BLENDMODE_DARKEN:
		result = BlendMode_Darken(base, blend);
		break;
		case BLENDMODE_MULTIPLY:
		result = BlendMode_Multiply(base, blend);
		break;
		case BLENDMODE_COLOR_BURN:
		result = BlendMode_ColorBurn(base, blend);
		break;
		case BLENDMODE_LINEAR_BURN:
		result = BlendMode_LinearBurn(base, blend);
		break;

		// Lighten modes
		case BLENDMODE_LIGHTEN:
		result = BlendMode_Lighten(base, blend);
		break;
		case BLENDMODE_SCREEN:
		result = BlendMode_Screen(base, blend);
		break;
		case BLENDMODE_COLOR_DODGE:
		result = BlendMode_ColorDodge(base, blend);
		break;
		case BLENDMODE_LINEAR_DODGE:
		result = BlendMode_LinearDodge(base, blend);
		break;

		// Contrast modes
		case BLENDMODE_OVERLAY:
		result = BlendMode_Overlay(base, blend);
		break;
		case BLENDMODE_SOFT_LIGHT:
		result = BlendMode_SoftLight(base, blend);
		break;
		case BLENDMODE_HARD_LIGHT:
		result = BlendMode_HardLight(base, blend);
		break;
		case BLENDMODE_VIVID_LIGHT:
		result = BlendMode_VividLight(base, blend);
		break;
		case BLENDMODE_LINEAR_LIGHT:
		result = BlendMode_LinearLight(base, blend);
		break;
		case BLENDMODE_PIN_LIGHT:
		result = BlendMode_PinLight(base, blend);
		break;
		case BLENDMODE_HARD_MIX:
		result = BlendMode_HardMix(base, blend);
		break;

		// Comparative modes
		case BLENDMODE_DIFFERENCE:
		result = BlendMode_Difference(base, blend);
		break;
		case BLENDMODE_EXCLUSION:
		result = BlendMode_Exclusion(base, blend);
		break;
		case BLENDMODE_SUBTRACT:
		result = BlendMode_Subtract(base, blend);
		break;
		case BLENDMODE_DIVIDE:
		result = BlendMode_Divide(base, blend);
		break;
	}
	return result;
}

float4
BlendModeStraight(int blendMode, float4 base, float4 blend)
{
	// Force converting to gamma space to reproduce classic blending in photoshop/gimp etc.
	#if BLEND_FORCEGAMMASPACE
	if (!IsGammaSpace())
	{
		base.rgb = LinearToGammaSpace(base);
		blend.rgb = LinearToGammaSpace(blend);
	}
	#endif

	float4 result = blend.a;
	result.rgb = BlendMode(blendMode, base, blend).rgb;
	result = saturate(result);

	#if BLEND_FORCEGAMMASPACE
	if (!IsGammaSpace())
	{
		result.rgb = GammaToLinearSpace(result);
	}
	#endif

	return result;
}

float4
BlendModePremultiplied(int blendMode, float4 base, float4 blend)
{
	// Must convert from premultiplied to straight before applying blend modes.
	base = saturate(ToStraight(base));
	blend = saturate(ToStraight(blend));

	float4 result = BlendModeStraight(blendMode, base, blend);

	// And convert back to premultiplied.
	result = ToPremultiplied(result);

	return result;
}

half4 
DoBlend(int blendMode, float4 base, float4 blend)
{
	half4 result = 0.0;
	switch (blendMode)
	{
		case BLENDMODE_REPLACE:
		result = AlphaComp_In(blend, base);
		break;
		case BLENDMODE_OVER:
		result = AlphaComp_Over(blend, base);
		break;
		case BLENDMODE_BLEND:
		result = AlphaComp_ATop(blend, base);
		break;
		case BLENDMODE_UNDER:
		result = AlphaComp_Over(base, blend);
		break;
		case BLENDMODE_CLEAR:
		result = base * (1.0 - blend.a);
		break;
		case BLENDMODE_MASK:
		result = base * blend.a;
		break;
		default:
		result = BlendModePremultiplied(blendMode, base, blend);
		result = AlphaComp_ATop(result, base);
		break;
	}
	return result;
}