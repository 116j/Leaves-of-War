//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public enum DissolveEdgeColorMode
	{
		None,
		Color,
		Ramp,
	}

	public enum DissolveDirection
	{
		None = 0,
		Horizontal = 1,
		Vertical = 2,
		MirrorX = 10,
		MirrorY = 11,
		Diagonal = 20,
		Circle = 30,

		Texture = 1000,
	}

	/// <summary>
	/// </summary>
	[AddComponentMenu(FilterBase.AddComponentsFiltersPath + "UIFX - Dissolve Filter")]
	public class DissolveFilter : FilterBase
	{
		[SerializeField] FillSpace _fillSpace = FillSpace.Geometry;
		[SerializeField] Texture _texture = null;
		[SerializeField] ScaleMode _textureScaleMode = ScaleMode.ScaleAndCrop;
		[SerializeField] float _scale = 1f;
		[SerializeField] bool _invert = false;
		[SerializeField] DissolveDirection _direction = DissolveDirection.None;
		[SerializeField] Texture _directionTexture = null;
		[Range(0f, 4f)]
		[SerializeField] float _directionStrength = 1f;
		[SerializeField] bool _directionInvert = false;
		[Range(0f, 1f)]
		[SerializeField] float _edgeLength = 0.1f;
		[SerializeField] DissolveEdgeColorMode _edgeColorMode;
		[ColorUsageAttribute(showAlpha: false, hdr: false)]
		[SerializeField] Color _edgeColor = Color.black;
		[SerializeField] Texture _edgeTexture = null;
		[Range(0f, 100f)]
		[SerializeField] float _edgeEmissive = 0f;

		public FillSpace FillSpace { get { return _fillSpace; } set { ChangePropertyEnum(ref _fillSpace, value); } }
		public Texture Texture { get { return _texture; } set { ChangePropertyRef(ref _texture, value); } }
		public ScaleMode TextureScaleMode { get { return _textureScaleMode; } set { ChangePropertyEnum(ref _textureScaleMode, value); } }
		public float TextureScale { get { return _scale; } set { ChangeProperty(ref _scale, value); } }
		public bool TextureInvert { get { return _invert; } set { ChangeProperty(ref _invert, value); } }
		public DissolveDirection Direction { get { return _direction; } set { ChangePropertyEnum(ref _direction, value); } }
		public Texture DirectionTexture { get { return _directionTexture; } set { ChangePropertyRef(ref _directionTexture, value); } }
		public float DirectionStrength { get { return _directionStrength; } set { ChangeProperty(ref _directionStrength, value); } }
		public bool DirectionInvert { get { return _directionInvert; } set { ChangeProperty(ref _directionInvert, value); } }
		public float EdgeLength { get { return _edgeLength; } set { ChangeProperty(ref _edgeLength, value); } }
		public DissolveEdgeColorMode EdgeColorMode { get { return _edgeColorMode; } set { ChangePropertyEnum(ref _edgeColorMode, value); } }
		public Color EdgeColor { get { return _edgeColor; } set { ChangeProperty(ref _edgeColor, value); } }
		public Texture EdgeTexture { get { return _edgeTexture; } set { ChangePropertyRef(ref _edgeTexture, value); } }
		public float EdgeEmissive { get { return _edgeEmissive; } set { ChangeProperty(ref _edgeEmissive, value); } }

		static new class ShaderProp
		{
			public readonly static int Dissolve = Shader.PropertyToID("_Dissolve");
			public readonly static int FillTex = Shader.PropertyToID("_FillTex");
			public readonly static int DirMaskTex = Shader.PropertyToID("_DirMaskTex");
			public readonly static int DirectionStrength = Shader.PropertyToID("_DirectionStrength");
			public readonly static int EdgeTex = Shader.PropertyToID("_EdgeTex");
			public readonly static int EdgeColor = Shader.PropertyToID("_EdgeColor");
			public readonly static int EdgeEmissive = Shader.PropertyToID("_EdgeEmissive");
			public readonly static int InvertFactor = Shader.PropertyToID("_InvertFactor");
			public readonly static int MaskRange = Shader.PropertyToID("_MaskRange");
			public readonly static int Direction = Shader.PropertyToID("_Direction");
			public readonly static int ScreenRect = Shader.PropertyToID("_ScreenRect");
		}

		static class ShaderKeyword
		{
			public const string EdgeColor = "EDGE_COLOR";
			public const string EdgeRamp = "EDGE_RAMP";
			public const string DirTexture = "DIR_TEXTURE";
			public const string DirProcedural = "DIR_PROCEDURAL";
			public const string ScreenSpace = "SCREEN_SPACE";
		}
		
		#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		static void RegisterUsedShaders()
		{
			ShaderUsageRegistry.Register(nameof(DissolveFilter), "Dissolve", ShaderUsageCategory.Filters, new[]
			{
				Compositor.CompositeShaderPath, FilterBase.ResolveShaderPath,
				BlendShaderPath
			});
		}
		#endif

		private const string BlendShaderPath = "Hidden/ChocDino/UIFX/Blend-Dissolve";

		protected override string GetDisplayShaderPath()
		{
			return BlendShaderPath;
		}

		protected override void Awake()
		{
			_expand = FilterExpand.None;
			base.Awake();
		}

		public override bool DoParametersHideSource()
		{
			return _strength >= 1f;
		}

		public override bool IsOutputHdr()
		{
			if (_edgeEmissive > 0f)
			{
				return true;
			}
			return base.IsOutputHdr();
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			// Calculate scale and offset values for our texture to fit it within the geometry rectangle with various layout controls
			Rect textureAspectAdjust = Rect.zero;
			if (_texture)
			{
				Rect geometryRect = _screenRect.GetRect();
				// NOTE: Added the following line as it fixes the juddering when moving/scaling across subpixels.
				geometryRect = new Rect(Mathf.FloorToInt(geometryRect.xMin), Mathf.FloorToInt(geometryRect.yMin), Mathf.CeilToInt(geometryRect.xMax) - Mathf.FloorToInt(geometryRect.xMin), Mathf.CeilToInt(geometryRect.yMax) - Mathf.FloorToInt(geometryRect.yMin));
				if (_fillSpace == FillSpace.Screen && _renderSpace == FilterRenderSpace.Screen)
				{
					geometryRect = _canvas.pixelRect;
				}

				float textureAspect = (float)_texture.width / (float)_texture.height;
				Rect aspectGeometryRect = MathUtils.ResizeRectToAspectRatio(geometryRect, _textureScaleMode, textureAspect);
				textureAspectAdjust = MathUtils.GetRelativeRect(geometryRect, aspectGeometryRect);

				// Scale centrally
				{
					textureAspectAdjust.x -= 0.5f;
					textureAspectAdjust.y -= 0.5f;
					textureAspectAdjust.xMin *= _scale;
					textureAspectAdjust.yMin *= _scale;
					textureAspectAdjust.xMax *= _scale;
					textureAspectAdjust.yMax *= _scale;
					textureAspectAdjust.x += 0.5f;
					textureAspectAdjust.y += 0.5f;
				}
			}

			{
				if (_fillSpace == FillSpace.Screen && _renderSpace == FilterRenderSpace.Screen)
				{
					Rect r = GetScreenRect();
					Vector4 v = new Vector4(r.x, r.y, r.width, r.height);
					_displayMaterial.SetVector(ShaderProp.ScreenRect, v);
					_displayMaterial.EnableKeyword(ShaderKeyword.ScreenSpace);
				}
				else
				{
					_displayMaterial.SetVector(ShaderProp.ScreenRect, new Vector4(0f, 0f, 1f, 1f));
					_displayMaterial.DisableKeyword(ShaderKeyword.ScreenSpace);
				}
			}

			{
				Vector2 invert;
				invert.x = (_texture != null && _invert) ? 1f : 0f;
				invert.y = _directionInvert ? 1f : 0f;
				_displayMaterial.SetVector(ShaderProp.InvertFactor, invert);
			}

			_displayMaterial.SetTexture(ShaderProp.FillTex, _texture);
			if (_texture != null)
			{
				_displayMaterial.SetTextureScale(ShaderProp.FillTex, new Vector2(textureAspectAdjust.width, textureAspectAdjust.height));
				_displayMaterial.SetTextureOffset(ShaderProp.FillTex, new Vector2(textureAspectAdjust.x, textureAspectAdjust.y));
				_displayMaterial.SetFloat(ShaderProp.MaskRange, 1f);
			}
			else
			{
				_displayMaterial.SetFloat(ShaderProp.MaskRange, 0f);
			}

			if (_directionStrength > 0f && _direction != DissolveDirection.None)
			{
				switch (_direction)
				{
					case DissolveDirection.Texture:
						_displayMaterial.DisableKeyword(ShaderKeyword.DirProcedural);
						_displayMaterial.EnableKeyword(ShaderKeyword.DirTexture);
						_displayMaterial.SetTexture(ShaderProp.DirMaskTex, _directionTexture);
						_displayMaterial.SetFloat(ShaderProp.DirectionStrength, _directionStrength);
						break;
					case DissolveDirection.Horizontal:
					case DissolveDirection.Vertical:
					case DissolveDirection.MirrorX:
					case DissolveDirection.MirrorY:
					case DissolveDirection.Diagonal:
					case DissolveDirection.Circle:
						_displayMaterial.EnableKeyword(ShaderKeyword.DirProcedural);
						_displayMaterial.DisableKeyword(ShaderKeyword.DirTexture);
						_displayMaterial.SetFloat(ShaderProp.Direction, (int)_direction);
						_displayMaterial.SetFloat(ShaderProp.DirectionStrength, _directionStrength);
						break;
				}
			}
			else
			{
				_displayMaterial.DisableKeyword(ShaderKeyword.DirTexture);
				_displayMaterial.DisableKeyword(ShaderKeyword.DirProcedural);
			}

			// Remap [0..1] range to [-edgeLength..1.0]
			_displayMaterial.SetVector(ShaderProp.Dissolve, new Vector3(0f, Mathf.Max(0.001f, _edgeLength), _strength));

			if (_edgeLength > 0 && _edgeColorMode != DissolveEdgeColorMode.None)
			{
				switch (_edgeColorMode)
				{
					case DissolveEdgeColorMode.Color:
						_displayMaterial.EnableKeyword(ShaderKeyword.EdgeColor);
						_displayMaterial.DisableKeyword(ShaderKeyword.EdgeRamp);
						_displayMaterial.SetColor(ShaderProp.EdgeColor, _edgeColor);
						_displayMaterial.SetFloat(ShaderProp.EdgeEmissive, _edgeEmissive + 1f);
						break;
					case DissolveEdgeColorMode.Ramp:
						_displayMaterial.DisableKeyword(ShaderKeyword.EdgeColor);
						_displayMaterial.EnableKeyword(ShaderKeyword.EdgeRamp);
						_displayMaterial.SetTexture(ShaderProp.EdgeTex, _edgeTexture);
						_displayMaterial.SetFloat(ShaderProp.EdgeEmissive, _edgeEmissive + 1f);
						break;
				}
			}
			else
			{
				_displayMaterial.DisableKeyword(ShaderKeyword.EdgeColor);
				_displayMaterial.DisableKeyword(ShaderKeyword.EdgeRamp);
			}

			base.SetupDisplayMaterial(source, result);
		}
	}
}