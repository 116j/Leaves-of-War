//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public enum DropShadowMode
	{
		Default,
		Inset,
		Glow,
		Cutout,
	}

	/// <summary>
	/// A drop shadow filter for uGUI components
	/// </summary>
	[AddComponentMenu(FilterBase.AddComponentsFiltersPath + "UIFX - Drop Shadow Filter")]
	public class DropShadowFilter : FilterBase
	{
		[Tooltip("How much to downsample before blurring")]
		[SerializeField] Downsample _downSample = Downsample.Auto;

		[Tooltip("The maximum size of the blur kernel as a fraction of the diagonal length.  So 0.01 would be a kernel with pixel dimensions of 1% of the diagonal length.")]
		[Range(0f, 256f)]
		[SerializeField] float _blur = 8f;
		
		[Tooltip("The transparency of the source content. Set to zero to make only the outline show.")]
		[Range(0f, 1f)]
		[SerializeField] float _sourceAlpha = 1.0f;

		[Tooltip("The clockwise angle the shadow is cast at. Range is [0..360]. Default is 135.0")]
		[Range(0f, 360f)]
		[SerializeField] float _angle = 135f;

		[Tooltip("The distance the shadow is cast. Range is [0..1]. Default is 0.03")]
		[Range(0f, 256f)]
		[SerializeField] float _distance = 8f;

		[Tooltip("")]
		[Range(-128f, 128f)]
		[SerializeField] float _spread = 0f;

		[Tooltip("The hardness of the shadow [0..4]. Default is 1.0")]
		[Range(0f, 4f)]
		[SerializeField] float _hardness = 1f;

		[Tooltip("The color of the shadow")]
		[SerializeField] Color _color = Color.black;

		[Tooltip("The mode to use for rendering. Default casts the shadow outside, Inset casts the shadow inside.")]
		[SerializeField] DropShadowMode _mode = DropShadowMode.Default;

		/// <summary>How much to downsample before blurring</summary>
		public Downsample Downsample { get { return _downSample; } set { ChangePropertyEnum(ref _downSample, value); } }

		/// <summary>The maximum size of the blur kernel as a fraction of the diagonal length.  So 0.01 would be a kernel with pixel dimensions of 1% of the diagonal length.</summary>
		public float Blur { get { return _blur; } set { ChangeProperty(ref _blur, value); } }

		/// <summary>The transparency of the source content. Set to zero to make only the outline show. Range is [0..1] Default is 1.0</summary>
		public float SourceAlpha { get { return _sourceAlpha; } set { ChangeProperty(ref _sourceAlpha, Mathf.Clamp01(value)); } }

		/// <summary>The clockwise angle the shadow is cast at. Range is [0..360]. Default is 135.0</summary>
		public float Angle { get { return _angle; } set { ChangeProperty(ref _angle, value); } }

		/// <summary>The distance the shadow is cast. Range is [0..1]. Default is 0.03</summary>
		public float Distance { get { return _distance; } set { ChangeProperty(ref _distance, value); } }

		/// <summary></summary>
		public float Spread { get { return _spread; } set { ChangeProperty(ref _spread, value); } }

		/// <summary>The hardness of the shadow [0..2]. Default is 0.5</summary>
		public float Hardness { get { return _hardness; } set { ChangeProperty(ref _hardness, value); } }

		/// <summary>The color of the shadow</summary>
		public Color Color { get { return _color; } set { ChangeProperty(ref _color, value); } }

		/// <summary>The mode to use for rendering. Default casts the shadow outside, Inset casts the shadow inside.</summary>
		public DropShadowMode Mode { get { return _mode; } set { ChangePropertyEnum(ref _mode, value); } }

		private const string BlendDropShadowShaderPath = "Hidden/ChocDino/UIFX/Blend-DropShadow";

		private BoxBlurReference _blurfx = null;
		private ErodeDilate _erodeDilate = null;

		static new class ShaderProp
		{
			public readonly static int SourceAlpha = Shader.PropertyToID("_SourceAlpha");
			public readonly static int ShadowOffset = Shader.PropertyToID("_ShadowOffset");
			public readonly static int ShadowHardness = Shader.PropertyToID("_ShadowHardness");
			public readonly static int ShadowColor = Shader.PropertyToID("_ShadowColor");
		}
		static class ShaderKeyword
		{
			public const string Inset = "INSET";
			public const string Glow = "GLOW";
			public const string Cutout = "CUTOUT";
		}

		#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		static void RegisterUsedShaders()
		{
			ShaderUsageRegistry.Register(nameof(DropShadowFilter), "Drop Shadow", ShaderUsageCategory.Filters, new []
			{
				Compositor.CompositeShaderPath, FilterBase.ResolveShaderPath,
				BlendDropShadowShaderPath, BoxBlurReference.BlurShader.Id, ErodeDilate.ShaderId
			});
		}
		#endif
		
		internal override bool CanApplyFilter()
		{
			if (_blurfx == null) return false;
			if (_erodeDilate == null) return false;
			return base.CanApplyFilter();
		}

		protected override bool DoParametersModifySource()
		{
			if (_sourceAlpha < 1f) return true;
			if (_hardness <= 0f) return false;
			if (_color.a <= 0f) return false;
			if (this.Strength <= 0f) return false;
			if (_spread > 0f && _mode != DropShadowMode.Inset) return true;
			if (_spread < 0f && _mode == DropShadowMode.Inset) return true;
			if (_distance > 0f) return true;
			if (_blur <= 0f) return false;
			return base.DoParametersModifySource();
		}

		protected override string GetDisplayShaderPath()
		{
			return BlendDropShadowShaderPath;
		}

		protected override void OnEnable()
		{
			_blurfx = new BoxBlurReference();
			_erodeDilate = new ErodeDilate();
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			if (_erodeDilate != null)
			{
				_erodeDilate.FreeResources();
				_erodeDilate = null;
			}
			if (_blurfx != null)
			{
				_blurfx.FreeResources();
				_blurfx = null;
			}
			base.OnDisable();
		}
		
		#if UNITY_EDITOR
		protected override void OnValidate()
		{
			// OnValidate is called when the scene is saved, which causes the material (fields without properties) to lose their properties, so we force update them here.
			if (_blurfx != null)
			{
				_blurfx.ForceDirty();
			}
			if (_erodeDilate != null)
			{
				_erodeDilate.ForceDirty();
			}
			
			base.OnValidate();
		}
		#endif
		
		private static Vector2 AngleToOffset(float angle, Vector2 scale)
		{
			return new Vector2(Mathf.Sin(-angle * Mathf.Deg2Rad) * scale.x, Mathf.Cos(-angle * Mathf.Deg2Rad + Mathf.PI) * scale.y);
		}

		protected override void GetFilterAdjustSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			float maxOffsetDistance = _distance * _strength * ResolutionScalingFactor;
			if (maxOffsetDistance > 0f)
			{
				Vector2 offset = -AngleToOffset(_angle, Vector2.one) * maxOffsetDistance;
				leftDown += new Vector2Int(Mathf.CeilToInt(Mathf.Abs(Mathf.Min(0f, offset.x))), Mathf.CeilToInt(Mathf.Abs(Mathf.Min(0f, offset.y))));
				rightUp += new Vector2Int(Mathf.CeilToInt(Mathf.Max(0f, offset.x)), Mathf.CeilToInt(Mathf.Max(0f, offset.y)));
			}
			if (_blurfx != null && _erodeDilate != null)
			{
				SetupFilterParams();
				_blurfx.AdjustBoundsSize(ref leftDown, ref rightUp);
				_erodeDilate.AdjustBoundsSize(ref leftDown, ref rightUp);
			}
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			{
				Vector2 shadowPixelOffset = AngleToOffset(_angle, Vector2.one);
				shadowPixelOffset *= _distance * _strength;
				shadowPixelOffset *= ResolutionScalingFactor;
				Vector2 texelStep = new Vector2(1f / source.width, 1f / source.height);
				shadowPixelOffset *= texelStep;
				_displayMaterial.SetVector(ShaderProp.ShadowOffset, shadowPixelOffset);
			}

			_displayMaterial.SetFloat(ShaderProp.SourceAlpha, Mathf.LerpUnclamped(1f, _sourceAlpha, this.Strength));
			_displayMaterial.SetFloat(ShaderProp.ShadowHardness, _hardness);

			{
				//color.a = Mathf.LerpUnclamped(0f, color.a, this.Strength);
				Color premultiplied = _color;
				premultiplied.r *= premultiplied.a;
				premultiplied.g *= premultiplied.a;
				premultiplied.b *= premultiplied.a;
				_displayMaterial.SetColor(ShaderProp.ShadowColor, premultiplied);
			}

			switch(_mode)
			{
				case DropShadowMode.Default:
					_displayMaterial.DisableKeyword(ShaderKeyword.Inset);
					_displayMaterial.DisableKeyword(ShaderKeyword.Glow);
					_displayMaterial.DisableKeyword(ShaderKeyword.Cutout);
					break;
				case DropShadowMode.Inset:
					_displayMaterial.EnableKeyword(ShaderKeyword.Inset);
					_displayMaterial.DisableKeyword(ShaderKeyword.Glow);
					_displayMaterial.DisableKeyword(ShaderKeyword.Cutout);
					break;
				case DropShadowMode.Glow:
					_displayMaterial.DisableKeyword(ShaderKeyword.Inset);
					_displayMaterial.EnableKeyword(ShaderKeyword.Glow);
					_displayMaterial.DisableKeyword(ShaderKeyword.Cutout);
					break;
				case DropShadowMode.Cutout:
					_displayMaterial.DisableKeyword(ShaderKeyword.Inset);
					_displayMaterial.DisableKeyword(ShaderKeyword.Glow);
					_displayMaterial.EnableKeyword(ShaderKeyword.Cutout);
					break;
			}

			base.SetupDisplayMaterial(source, result);
		}

		private void SetupFilterParams()
		{
			_blurfx.IterationCount = 2;
			_blurfx.Downsample = _downSample;
			_blurfx.SetBlurSize(_blur * _strength * ResolutionScalingFactor);

			_erodeDilate.AlphaOnly = false;
			_erodeDilate.UseMultiPassOptimisation = true;
			_erodeDilate.ErodeSize = -Mathf.Min(0f, _spread) * _strength * ResolutionScalingFactor;
			_erodeDilate.DilateSize = Mathf.Max(0f, _spread) * _strength * ResolutionScalingFactor;
		}

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			if (_blur > 0f || _spread != 0f)
			{
				SetupFilterParams();
				if (_spread != 0f)
				{
					source = _erodeDilate.Process(source);
				}
			
				source = _blurfx.Process(source);
				
				return source;
			}
			return source;
		}
	}
}