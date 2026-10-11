//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

#if UNITY_2019_4_OR_NEWER
// Enum x has multiple entries with the same display name, this prevents selection in EnumPopup.
#define UNITY_WARNING_MULTIPLE_INSPECTORNAME
#endif

using UnityEngine;

namespace ChocDino.UIFX
{
	/// <summary>Fill modes for the FillColorFilter component.</summary>
	public enum FillColorMode
	{
		/// <summary>A single solid color.</summary>
		Solid,
		/// <summary>Two colors - one on the left edge, and one on the right edge.</summary>
		Horizontal,
		/// <summary>Two colors - one on the top edge, and one on the bottom edge.</summary>
		Vertical,
		/// <summary>Four colors at each of the corners of the rectangle, creating a gradient fill effect.</summary>
		Corners,
	}

	public enum FillSpace
	{
		Geometry,
		Screen,
	}

	public enum FillBlendMode
	{
		// Normal
		Replace = 0,
		Blend,
		Over,
		Under,
		Mask,
		Clear,

		// Darken
#if !UNITY_WARNING_MULTIPLE_INSPECTORNAME
		[InspectorName(null)]
		_SectionDarken = 1000,
#endif
		Darken = 10,
		Multiply,
		ColorBurn,
		LinearBurn,

		// Lighten
#if !UNITY_WARNING_MULTIPLE_INSPECTORNAME
		[InspectorName(null)]
		_SectionLighten = 2000,
#endif
		Lighten = 20,
		Screen,
		ColorDodge,
		LinearDodge,

		// Contrast
#if !UNITY_WARNING_MULTIPLE_INSPECTORNAME
		[InspectorName(null)]
		_SectionContrast = 5000,
#endif
		Overlay = 30,
		SoftLight,
		HardLight,
		VividLight,
		LinearLight,
		PinLight,
		HardMix,

		// Comparative
#if !UNITY_WARNING_MULTIPLE_INSPECTORNAME
		[InspectorName(null)]
		_SectionComparative = 5000,
#endif
		Difference = 50,
		Exclusion,
		Subtract,
		Divide,
	}

	/// <summary>
	/// A visual filter that fills a uGUI component with a color.
	/// </summary>
	[AddComponentMenu(FilterBase.AddComponentsFiltersPath + "Fill/UIFX - Fill Color Filter")]
	public class FillColorFilter : FilterBase
	{
		[Tooltip("The fill mode to use.")]
		[SerializeField] FillColorMode _mode = FillColorMode.Solid;

		[Tooltip("The color of the fill in Solid mode.")]
		[SerializeField] Color _color = Color.red;

		[Tooltip("The color of top edge in Vertical mode. The color of the left edge in Horizontal mode.")]
		[SerializeField] Color _colorA = Color.red;

		[Tooltip("The color of bottom edge in Vertical mode. The color of the right edge in Horizontal mode.")]
		[SerializeField] Color _colorB = Color.blue;

		[Tooltip("The color of top-left corner in Corners mode.")]
		[SerializeField] Color _colorTL = Color.red;

		[Tooltip("The color of top-right corner in Corners mode.")]
		[SerializeField] Color _colorTR = Color.green;

		[Tooltip("The color of bottom-left corner in Corners mode.")]
		[SerializeField] Color _colorBL = Color.blue;

		[Tooltip("The color of bottom-right corner in Corners mode.")]
		[SerializeField] Color _colorBR = Color.magenta;

		[Tooltip("Used to scale the positions of the edge/corner colors in Horizonal/Vertical/Corners mode. Higher values move the colors towards the center of the rectangle.")]
		[SerializeField] float _colorScale = 1f;

		[Tooltip("Used to bias the position of the edge colors in Horizonal/Vertical mode.")]
		[Range(-1f, 1f)]
		[SerializeField] float _colorBias = 0f;

		[Tooltip("")]
		[SerializeField] FillSpace _fillSpace = FillSpace.Geometry;

		[Tooltip("How to composite the fill with the source graphic.")]
		[SerializeField] FillBlendMode _blendMode = FillBlendMode.Blend;

		[Tooltip("Whether to force blend operations in happen gamma-space to match the default in software such as PhotoShop. Otherwise blend operations happen in the active color-space.")]
		[SerializeField] bool _blendModeForceGammaSpace = false;

		/// <summary>The fill mode to use.</summary>
		public FillColorMode Mode { get { return _mode; } set { ChangePropertyEnum(ref _mode, value); } }

		/// <summary>The color of the fill in Solid mode.</summary>
		public Color Color { get { return _color; } set { ChangeProperty(ref _color, value); } }

		/// <summary>The color of top edge in Vertical mode. The color of the left edge in Horizontal mode.</summary>
		public Color ColorA { get { return _colorA; } set { ChangeProperty(ref _colorA, value); } }

		/// <summary>The color of bottom edge in Vertical mode. The color of the right edge in Horizontal mode.</summary>
		public Color ColorB { get { return _colorB; } set { ChangeProperty(ref _colorB, value); } }

		/// <summary>The color of top-left corner in Corners mode.</summary>
		public Color ColorTL { get { return _colorTL; } set { ChangeProperty(ref _colorTL, value); } }

		/// <summary>The color of top-right corner in Qorners mode.</summary>
		public Color ColorTR { get { return _colorTR; } set { ChangeProperty(ref _colorTR, value); } }

		/// <summary>The color of bottom-left corner in Corners mode.</summary>
		public Color ColorBL { get { return _colorBL; } set { ChangeProperty(ref _colorBL, value); } }

		/// <summary>The color of bottom-right corner in Corners mode.</summary>
		public Color ColorBR { get { return _colorBR; } set { ChangeProperty(ref _colorBR, value); } }

		/// <summary>Used to scale the positions of the corner colors in Horizonal/Vertical/Corners mode. Higher values move the colors towards the center of the rectangle.</summary>
		public float ColorScale { get { return _colorScale; } set { ChangeProperty(ref _colorScale, value); } }

		/// <summary>Used to bias the position of the edge colors in Horizonal/Vertical mode.</summary>
		public float ColorBias { get { return _colorScale; } set { ChangeProperty(ref _colorBias, value); } }

		/// <summary></summary>
		public FillSpace FillSpace { get { return _fillSpace; } set { ChangePropertyEnum(ref _fillSpace, value); } }

		/// <summary>How to composite the fill with the source graphic.</summary>
		public FillBlendMode BlendMode { get { return _blendMode; } set { ChangePropertyEnum(ref _blendMode, value); } }

		/// <summary>Whether to force blend operations in happen gamma-space to match the default in software such as PhotoShop. Otherwise blend operations happen in the active color-space.</summary>
		public bool BlendModeForceGammaSpace { get { return _blendModeForceGammaSpace; } set { ChangeProperty(ref _blendModeForceGammaSpace, value); } }

		static new class ShaderProp
		{
			public readonly static int FillColor = Shader.PropertyToID("_FillColor");
			public readonly static int FillColorA = Shader.PropertyToID("_FillColorA");
			public readonly static int FillColorB = Shader.PropertyToID("_FillColorB");
			public readonly static int FillColorTL = Shader.PropertyToID("_FillColorTL");
			public readonly static int FillColorTR = Shader.PropertyToID("_FillColorTR");
			public readonly static int FillColorBL = Shader.PropertyToID("_FillColorBL");
			public readonly static int FillColorBR = Shader.PropertyToID("_FillColorBR");
			public readonly static int ColorScaleBias = Shader.PropertyToID("_ColorScaleBias");
			public readonly static int BlendMode = Shader.PropertyToID("_BlendMode");
			public readonly static int ScreenRect = Shader.PropertyToID("_ScreenRect");
		}
		static class ShaderKeyword
		{
			public const string ModeHorizontal = "MODE_HORIZONTAL";
			public const string ModeVertical = "MODE_VERTICAL";
			public const string ModeCorners = "MODE_CORNERS";
			public const string BlendForceGammaSpace = "BLEND_FORCEGAMMASPACE";
		}

		#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		static void RegisterUsedShaders()
		{
			ShaderUsageRegistry.Register(nameof(FillColorFilter), "Fill Color", ShaderUsageCategory.Filters, new[]
			{
				Compositor.CompositeShaderPath, FilterBase.ResolveShaderPath,
				BlendShaderPath
			});
		}
		#endif
		
		private const string BlendShaderPath = "Hidden/ChocDino/UIFX/Blend-Fill-Color";

		protected override string GetDisplayShaderPath()
		{
			return BlendShaderPath;
		}

		protected override void OnEnable()
		{
			_expand = FilterExpand.None;
			base.OnEnable();
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			float bias = 0.5f + _colorBias * ((Mathf.Abs(_colorScale) * 0.5f) + 0.5f);
			
			switch (_mode)
			{
				case FillColorMode.Solid:
				_displayMaterial.SetColor(ShaderProp.FillColor, _color);
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeHorizontal);
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeVertical);
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeCorners);
				break;
				case FillColorMode.Horizontal:
				_displayMaterial.SetColor(ShaderProp.FillColorA, _colorA);
				_displayMaterial.SetColor(ShaderProp.FillColorB, _colorB);
				_displayMaterial.EnableKeyword(ShaderKeyword.ModeHorizontal);
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeVertical);
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeCorners);
				_displayMaterial.SetVector(ShaderProp.ColorScaleBias, new Vector2(_colorScale, bias));
				break;
				case FillColorMode.Vertical:
				_displayMaterial.SetColor(ShaderProp.FillColorA, _colorA);
				_displayMaterial.SetColor(ShaderProp.FillColorB, _colorB);
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeHorizontal);
				_displayMaterial.EnableKeyword(ShaderKeyword.ModeVertical);
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeCorners);
				_displayMaterial.SetVector(ShaderProp.ColorScaleBias, new Vector2(_colorScale, bias));
				break;
				case FillColorMode.Corners:
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeHorizontal);
				_displayMaterial.DisableKeyword(ShaderKeyword.ModeVertical);
				_displayMaterial.EnableKeyword(ShaderKeyword.ModeCorners);
				_displayMaterial.SetColor(ShaderProp.FillColorTL, _colorTL);
				_displayMaterial.SetColor(ShaderProp.FillColorTR, _colorTR);
				_displayMaterial.SetColor(ShaderProp.FillColorBL, _colorBL);
				_displayMaterial.SetColor(ShaderProp.FillColorBR, _colorBR);
				_displayMaterial.SetVector(ShaderProp.ColorScaleBias, new Vector2(_colorScale, bias));
				break;
			}

			_displayMaterial.SetInt(ShaderProp.BlendMode, (int)_blendMode);
			if (!_blendModeForceGammaSpace)
			{
				_displayMaterial.DisableKeyword(ShaderKeyword.BlendForceGammaSpace);
			}
			else
			{
				_displayMaterial.EnableKeyword(ShaderKeyword.BlendForceGammaSpace);
			}

			if (_fillSpace == FillSpace.Screen && _renderSpace == FilterRenderSpace.Screen)
			{
				Rect r = GetScreenRect();
				Vector4 v = new Vector4(r.x, r.y, r.width, r.height);
				_displayMaterial.SetVector(ShaderProp.ScreenRect, v);
			}
			else
			{
				_displayMaterial.SetVector(ShaderProp.ScreenRect, new Vector4(0f, 0f, 1f, 1f));
			}

			_displayMaterial.SetFloat(FilterBase.ShaderProp.Strength, _strength);

			base.SetupDisplayMaterial(source, result);
		}
	}
}