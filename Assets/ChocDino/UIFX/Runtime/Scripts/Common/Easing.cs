//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public enum EasePreset
	{
		Step,
		Linear,
		[InspectorName("Sine In\tSlow->Fast")]
		InSine,
		[InspectorName("Sine Out\tFast->Slow")]
		OutSine,
		[InspectorName("Sine In-Out\tSlow-Fast-Slow")]
		InOutSine,
		[InspectorName("Sine Out-In\tFast->Slow->Fast")]
		OutInSine,
		[InspectorName("Quad In\tSlow->Fast")]
		InQuad,
		[InspectorName("Quad Out\tFast->Slow")]
		OutQuad,
		[InspectorName("Quad In-Out\tSlow-Fast-Slow")]
		InOutQuad,
		[InspectorName("Quad Out-In\tFast->Slow->Fast")]
		OutInQuad,
		[InspectorName("Cubic In\tSlow->Fast")]
		InCubic,
		[InspectorName("Cubic Out\tFast->Slow")]
		OutCubic,
		[InspectorName("Cubic In-Out\tSlow-Fast-Slow")]
		InOutCubic,
		[InspectorName("Cubic Out-In\tFast->Slow->Fast")]
		OutInCubic,
		[InspectorName("Quint In\tSlow->Fast")]
		InQuint,
		[InspectorName("Quint Out\tFast->Slow")]
		OutQuint,
		[InspectorName("Quint In-Out\tSlow-Fast-Slow")]
		InOutQuint,
		[InspectorName("Quint Out-In\tFast->Slow->Fast")]
		OutInQuint,
		[InspectorName("Quart In\tSlow->Fast")]
		InQuart,
		[InspectorName("Quart Out\tFast->Slow")]
		OutQuart,
		[InspectorName("Quart In-Out\tSlow-Fast-Slow")]
		InOutQuart,
		[InspectorName("Quart Out-In\tFast->Slow->Fast")]
		OutInQuart,
		[InspectorName("Expo In\tSlow->Fast")]
		InExpo,
		[InspectorName("Expo Out\tFast->Slow")]
		OutExpo,
		[InspectorName("Expo In-Out\tSlow-Fast-Slow")]
		InOutExpo,
		[InspectorName("Expo Out-In\tFast->Slow->Fast")]
		OutInExpo,
		[InspectorName("Circ In\tSlow->Fast")]
		InCirc,
		[InspectorName("Circ Out\tFast->Slow")]
		OutCirc,
		[InspectorName("Circ In-Out\tSlow-Fast-Slow")]
		InOutCirc,
		[InspectorName("Circ Out-In\tFast->Slow->Fast")]
		OutInCirc,
		// Overshoot easing modes:
		[InspectorName("Back In\tSlow->Fast")]
		InBack,
		[InspectorName("Back Out\tFast->Slow")]
		OutBack,
		[InspectorName("Back In-Out\tSlow->Fast->Slow")]
		InOutBack,
		[InspectorName("Back Out-In\tFast->Slow->Fast")]
		OutInBack,
		Bounce,
		BouncePast,
		Elastic,

		CustomElastic,
	}

	public delegate float EaseFunction(float t);

	/// <summary>
	/// Easing functions, inspired by various sources
	///
	/// t range is [0..1]
	///
	/// Generally the In Out prefixes mean:
	/// 	In = slow -> fast
	/// 	Out = fast -> slow
	/// 	InOut = slow -> fast -> slow
	/// 	OutIn = fast -> slow -> fast
	///
	/// We haven't added In/Out variants for bounce, elastic as we don't believe those would be useful
	/// </summary>
	[System.Serializable]
	public class Easing
	{
		#if false
		/*public enum Function
		{
			Step,
			Linear,
			Sine,
			Quad,
			Cubic,
			Quint,
			Quart,
			Expo,
			Circ,
			Back,
			Bounce,
			BouncePast,
			Elastic,
		}*/

		public enum Mode
		{
			In,
			Out,
			InOut,
			OutIn,
		}

		struct blah
		{
			public float backOvershoot = 1.70158f;
			public float elasticAmp = 1.0f;
			public float elasticFreq = 0.3f;
			public float inOutLerpPoint = 0.5f;
		}

		/*class Step
		{
			public static HasType { return false; }
			public float Evaluate(float t);
		}*/

		struct Blah
		{
			public Function function;
			public bool hasOut, hasInOut, hasOutIn,
		}

		EaseType.Step, false, false, false };
#endif

		[SerializeField] EasePreset _preset = EasePreset.Linear;

		public EasePreset Preset { get => _preset; set => _preset = value; }

		private EasePreset _functionPreset = EasePreset.Linear;
		private EaseFunction _function;

		public float Evaluate(float t)
		{
			if (_preset != _functionPreset)
			{
				_functionPreset = _preset;
				_function = GetFunction(_preset);
			}
			if (_function != null)
			{
				return _function(t);
			}
			return t;
		}

		// NOTE: Pre-allocate function delegates to prevent garbage
		private static EaseFunction funcStep = EaseStep;
		private static EaseFunction funcLinear = EaseLinear;
		private static EaseFunction funcSine = EaseSine;
		private static EaseFunction funcSineOut = OutSine;
		private static EaseFunction funcSineInOut = InOutSine;
		private static EaseFunction funcSineOutIn = OutInSine;
		private static EaseFunction funcQuad = EaseQuad;
		private static EaseFunction funcQuadOut = OutQuad;
		private static EaseFunction funcQuadInOut = InOutQuad;
		private static EaseFunction funcQuadOutIn = OutInQuad;
		private static EaseFunction funcCubic = EaseCubic;
		private static EaseFunction funcCubicOut = OutCubic;
		private static EaseFunction funcCubicInOut = InOutCubic;
		private static EaseFunction funcCubicOutIn = OutInCubic;
		private static EaseFunction funcQuart = EaseQuart;
		private static EaseFunction funcQuartOut = OutQuart;
		private static EaseFunction funcQuartInOut = InOutQuart;
		private static EaseFunction funcQuartOutIn = OutInQuart;
		private static EaseFunction funcQuint = EaseQuint;
		private static EaseFunction funcQuintOut = OutQuint;
		private static EaseFunction funcQuintInOut = InOutQuint;
		private static EaseFunction funcQuintOutIn = OutInQuint;
		private static EaseFunction funcExpo = EaseExpo;
		private static EaseFunction funcExpoOut = OutExpo;
		private static EaseFunction funcExpoInOut = InOutExpo;
		private static EaseFunction funcExpoOutIn = OutInExpo;
		private static EaseFunction funcCirc = EaseCirc;
		private static EaseFunction funcCircOut = OutCirc;
		private static EaseFunction funcCircInOut = InOutCirc;
		private static EaseFunction funcCircOutIn = OutInCirc;
		private static EaseFunction funcBack = EaseBack;
		private static EaseFunction funcBackOut = OutBack;
		private static EaseFunction funcBackInOut = InOutBack;
		private static EaseFunction funcBackOutIn = OutInBack;
		private static EaseFunction funcBounce = EaseBounce;
		private static EaseFunction funcBouncePast = EaseBouncePast;
		private static EaseFunction funcElastic = EaseElastic;

		public static EaseFunction GetFunction(EasePreset preset)
		{
			EaseFunction result = null;
			switch (preset)
			{
				case EasePreset.Step:
					result = funcStep;
					break;
				case EasePreset.Linear:
					result = funcLinear;
					break;
				case EasePreset.InSine:
					result = funcSine;
					break;
				case EasePreset.OutSine:
					result = funcSineOut;
					break;
				case EasePreset.InOutSine:
					result = funcSineInOut;
					break;
				case EasePreset.OutInSine:
					result = funcSineOutIn;
					break;
				case EasePreset.InQuad:
					result = funcQuad;
					break;
				case EasePreset.OutQuad:
					result = funcQuadOut;
					break;
				case EasePreset.InOutQuad:
					result = funcQuadInOut;
					break;
				case EasePreset.OutInQuad:
					result = funcQuadOutIn;
					break;
				case EasePreset.InCubic:
					result = funcCubic;
					break;
				case EasePreset.OutCubic:
					result = funcCubicOut;
					break;
				case EasePreset.InOutCubic:
					result = funcCubicInOut;
					break;
				case EasePreset.OutInCubic:
					result = funcCubicOutIn;
					break;
				case EasePreset.InQuart:
					result = funcQuart;
					break;
				case EasePreset.OutQuart:
					result = funcQuartOut;
					break;
				case EasePreset.InOutQuart:
					result = funcQuartInOut;
					break;
				case EasePreset.OutInQuart:
					result = funcQuartOutIn;
					break;
				case EasePreset.InQuint:
					result = funcQuint;
					break;
				case EasePreset.OutQuint:
					result = funcQuintOut;
					break;
				case EasePreset.InOutQuint:
					result = funcQuintInOut;
					break;
				case EasePreset.OutInQuint:
					result = funcQuintOutIn;
					break;
				case EasePreset.InExpo:
					result = funcExpo;
					break;
				case EasePreset.OutExpo:
					result = funcExpoOut;
					break;
				case EasePreset.InOutExpo:
					result = funcExpoInOut;
					break;
				case EasePreset.OutInExpo:
					result = funcExpoOutIn;
					break;
				case EasePreset.InCirc:
					result = funcCirc;
					break;
				case EasePreset.OutCirc:
					result = funcCircOut;
					break;
				case EasePreset.InOutCirc:
					result = funcCircInOut;
					break;
				case EasePreset.OutInCirc:
					result = funcCircOutIn;
					break;
				case EasePreset.InBack:
					result = funcBack;
					break;
				case EasePreset.OutBack:
					result = funcBackOut;
					break;
				case EasePreset.InOutBack:
					result = funcBackInOut;
					break;
				case EasePreset.OutInBack:
					result = funcBackOutIn;
					break;
				case EasePreset.Bounce:
					result = funcBounce;
					break;
				case EasePreset.BouncePast:
					result = funcBouncePast;
					break;
				case EasePreset.Elastic:
					result = funcElastic;
					break;
				default:
					throw new System.Exception("Invalid enum " + preset);
			}
			return result;
		}

		public static float EaseStep(float t)
		{
			float result = 0f;
			if (t >= 1f)
			{
				result = 1f;
			}
			return result;
		}

		// TODO: Add staircase functions (https://mathworld.wolfram.com/StaircaseFunction.html)
		// TODO: Add smooth staircase functions (https://math.stackexchange.com/questions/1671132/equation-for-a-smooth-staircase-function)

		public static float EaseLinear(float t)
		{
			return t;
		}

		public static float EaseSine(float t)
		{
			return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
		}

		public static float EaseQuad(float t)
		{
			return t * t;
		}

		public static float EaseCubic(float t)
		{
			return t * t * t;
		}

		public static float EaseQuart(float t)
		{
			float t2 = t * t;
			return t2 * t2;
		}

		public static float EaseQuint(float t)
		{
			float t2 = t * t;
			return t2 * t2 * t;
		}

		public static float EaseExpo(float t)
		{
			float result = 0f;
			if (t != 0f)
			{
				result = Mathf.Pow(2f, 10f * (t - 1f));
			}
			return result;
		}

		public static float EaseCirc(float t)
		{
			return 1f - Mathf.Sqrt(1f - t * t);
		}

		public static float EaseBack(float t, float overshoot = 1.70158f)
		{
			return t * t * ((overshoot + 1f) * t - overshoot);
		}

		public static float EaseBack(float t)
		{
			return EaseBack(t, 1.70158f);
		}

		public static float EaseBounce(float t)
		{	
			const float n = 7.5625f;
			const float d = 2.75f;

			float result;
			if (t < 1.0f / d) 
			{
				result = n * t * t;
			}
			else if (t < (2.0f / d)) 
			{
				t -= 1.5f / d;
				result = n * t * t + 0.75f;
			}
			else if (t < (2.5f / d)) 
			{
				t -= 2.25f / d;
				result = n * t * t + 0.9375f;
			}
			else
			{
				t -= 2.625f / d;
				result = n * t * t + 0.984375f;
			}
			return result;
		}

		public static float EaseBouncePast(float t)
		{
			const float n = 7.5625f;
			const float d = 2.75f;

			float result;
			if (t < 1.0f / d) 
			{
				result = n * t * t;
			}
			else if (t < (2.0f / d)) 
			{
				t -= 1.5f / d;
				result = 2f - (n * t * t + 0.75f);
			}
			else if (t < (2.5f / d)) 
			{
				t -= 2.25f / d;
				result = 2f - (n * t * t + 0.9375f);
			}
			else
			{
				t -= 2.625f / d;
				result = 2f - (n * t * t + 0.984375f);
			}
			return result;
		}

		public static float EaseElastic(float t, float amplitude = 1f, float period = 0.3f)
		{
			float a = amplitude;
			float p = period;
			a = Mathf.Max(1f, a);
			p /= Mathf.PI * 2f;
 			float s = Mathf.Asin(1f / a) * p;
			float z = (Mathf.Pow(2f, -10f * t) - 0.0009765625f) * 1.0009775171065494f;
			return 1f - a * z * Mathf.Sin((s + t) / p);
		}

		public static float EaseElastic(float t)
		{
			return EaseElastic(t, 1f, 0.3f);
		}

		public static float InSine(float t)	{ return In(t, funcSine); }
		public static float OutSine(float t) { return Out(t, funcSine);	}
		public static float InOutSine(float t) { return InOut(t, funcSine); }
		public static float OutInSine(float t) { return OutIn(t, funcSine); }

		public static float InQuad(float t)	{ return In(t, funcQuad); }
		public static float OutQuad(float t) { return Out(t, funcQuad);	}
		public static float InOutQuad(float t) { return InOut(t, funcQuad); }
		public static float OutInQuad(float t) { return OutIn(t, funcQuad); }

		public static float InCubic(float t)	{ return In(t, funcCubic); }
		public static float OutCubic(float t) { return Out(t, funcCubic);	}
		public static float InOutCubic(float t) { return InOut(t, funcCubic); }
		public static float OutInCubic(float t) { return OutIn(t, funcCubic); }

		public static float InQuart(float t)	{ return In(t, funcQuart); }
		public static float OutQuart(float t) { return Out(t, funcQuart);	}
		public static float InOutQuart(float t) { return InOut(t, funcQuart); }
		public static float OutInQuart(float t) { return OutIn(t, funcQuart); }

		public static float InQuint(float t)	{ return In(t, funcQuint); }
		public static float OutQuint(float t) { return Out(t, funcQuint);	}
		public static float InOutQuint(float t) { return InOut(t, funcQuint); }
		public static float OutInQuint(float t) { return OutIn(t, funcQuint); }

		public static float InExpo(float t)	{ return In(t, funcExpo); }
		public static float OutExpo(float t) { return Out(t, funcExpo);	}
		public static float InOutExpo(float t) { return InOut(t, funcExpo); }
		public static float OutInExpo(float t) { return OutIn(t, funcExpo); }

		public static float InCirc(float t)	{ return In(t, funcCirc); }
		public static float OutCirc(float t) { return Out(t, funcCirc);	}
		public static float InOutCirc(float t) { return InOut(t, funcCirc); }
		public static float OutInCirc(float t) { return OutIn(t, funcCirc); }

		public static float InBack(float t)	{ return In(t, funcBack); }
		public static float OutBack(float t) { return Out(t, funcBack);	}
		public static float InOutBack(float t) { return InOut(t, funcBack); }
		public static float OutInBack(float t) { return OutIn(t, funcBack); }

		private static float In(float t, EaseFunction easeFunction)
		{
			return easeFunction.Invoke(t);
		}

		private static float Out(float t, EaseFunction easeFunction)
		{
			return 1f - easeFunction.Invoke(1f - t);
		}

		/// <summary></summary>
		/// <param name="t">The time value in range [0..1]</param>
		/// <param name="easeFunction">The function to use for easing</param>
		/// <param name="p">The position in range [0..1] at which the switch occurs</param>
		/// <returns>A value in range [0..1]</returns>
		private static float InOut(float t, EaseFunction easeFunction, float p = 0.5f)
		{
			float result = 0f;
			if (t > 0f)
			{
				result = 1f;
				if (t < 1f)
				{
					if (t < p)
					{
						// convert t to [0..1] range and scale result to [0..p] range
						result = In(t / p, easeFunction) * p;
					}
					else
					{
						// convert t to [0..1] range and scale result to [p..1] range
						result = Out((t - p) / (1f - p), easeFunction) * (1f - p) + p;
					}
				}
			}
			return result;
		}

		/// <summary></summary>
		/// <param name="t">The time value in range [0..1]</param>
		/// <param name="easeFunction">The function to use for easing</param>
		/// <param name="p">The position in range [0..1] at which the switch occurs</param>
		/// <returns>A value in range [0..1]</returns>
		private static float OutIn(float t, EaseFunction easeFunction, float p = 0.5f)
		{
			float result = 0f;
			if (t > 0f)
			{
				result = 1f;
				if (t < 1f)
				{
					if (t < p)
					{
						// convert t to [0..1] range and scale result to [0..p] range
						result = Out(t / p, easeFunction) * p;
					}
					else
					{
						// convert t to [0..1] range and scale result to [p..1] range
						result = In((t - p) / (1f - p), easeFunction) * (1f - p) + p;
					}
				}
			}
			return result;
		}
	}
}