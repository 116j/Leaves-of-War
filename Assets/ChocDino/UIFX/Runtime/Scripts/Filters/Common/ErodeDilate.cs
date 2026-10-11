//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public class ErodeDilate
	{
		internal const string ShaderId = "Hidden/ChocDino/UIFX/ErodeDilate";

		private static class ShaderProp
		{
			internal static int ErodeRadius = Shader.PropertyToID("_ErodeRadius");
			internal static int DilateRadius = Shader.PropertyToID("_DilateRadius");
		}
		private static class ShaderKeyword
		{
			public const string DistSquare = "DIST_SQUARE";
			public const string DistDiamond = "DIST_DIAMOND";
			public const string DistCircle = "DIST_CIRCLE";
		}
		private static class ShaderPass
		{
			internal const int ErodeAlpha = 0;
			internal const int DilateAlpha = 1;
			internal const int ErodeDilateAlpha = 2;
			internal const int Erode = 3;
			internal const int Dilate = 4;
			internal const int CopyAlpha = 5;
			internal const int Null = 6;
		}

		public float ErodeSize { get { return _erodeSize; } set { ChangeProperty(ref _erodeSize, value); } }
		public float DilateSize { get { return _dilateSize; } set { ChangeProperty(ref _dilateSize, value); } }
		public DistanceShape DistanceShape { get { return _distanceShape; } set { ChangePropertyEnum(ref _distanceShape, value); } }
		public bool AlphaOnly { get { return _alphaOnly; } set { ChangeProperty(ref _alphaOnly, value); } }
		public bool UseMultiPassOptimisation { get { return _useMultiPassOptimisation; } set { ChangeProperty(ref _useMultiPassOptimisation, value); } }

		internal RenderTexture OutputTexture { get { return _output; } }

		private float _erodeSize = 0f;
		private float _dilateSize = 0f;
		private DistanceShape _distanceShape = DistanceShape.Circle;
		private bool _alphaOnly = false;
		private bool _useMultiPassOptimisation = false;

		private Material _material;
		private RenderTexture _rtResult;
		private RenderTexture _rtResult2;
		private RenderTexture _sourceTexture;
		private RenderTexture _output;

		private bool _materialsDirty = true;

		public void ForceDirty()
		{
			_materialsDirty = true;
		}

		private void ChangeProperty<T>(ref T backing, T value) where T : struct
		{ 
			if (ObjectHelper.ChangeProperty(ref backing, value))
			{
				_materialsDirty = true;
			}
		}

		// NOTE: Implemented for bool to prevent garbage generation.
		protected void ChangeProperty(ref bool backing, bool value)
		{
			if (ObjectHelper.ChangeProperty(ref backing, value))
			{
				_materialsDirty = true;
			}
		}

		// NOTE: Implemented for Enum to prevent garbage generation.
		private void ChangePropertyEnum<T>(ref T backing, T value) where T : struct, System.Enum
		{
			if (ObjectHelper.ChangePropertyEnum(ref backing, value))
			{
				_materialsDirty = true;
			}
		}

		public void AdjustBoundsSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			float maxSize = _dilateSize;
			int size = Mathf.CeilToInt(maxSize);
			if (size > 0)
			{
				leftDown += new Vector2Int(size, size);
				rightUp += new Vector2Int(size, size);
			}
		}

		public RenderTexture Process(RenderTexture sourceTexture)
		{
			Debug.Assert(sourceTexture != null);

			// Early-out
			if (_erodeSize <= 0f && _dilateSize <= 0f)
			{
				FreeTextures();
				return sourceTexture;
			}

			RenderTexture prevRT = RenderTexture.active;

			SetupResources(sourceTexture);

			if (_materialsDirty)
			{
				UpdateMaterials();
			}

			RenderTexture rtAlphaSource = null;
			RenderTexture source = _sourceTexture;
			_output = _sourceTexture;

			if (_alphaOnly)
			{
				rtAlphaSource = RenderTexture.GetTemporary(_rtResult.width, _rtResult.height, 0, _rtResult.format, RenderTextureReadWrite.Linear);
				rtAlphaSource.SetDebugName("AlphaSource");
				Graphics.Blit(_sourceTexture, rtAlphaSource, _material, ShaderPass.CopyAlpha);
				rtAlphaSource.IncrementUpdateCount();
				source = rtAlphaSource;
			}

			if (_useMultiPassOptimisation)
			{
				float dilateSize = _dilateSize;
				float erodeSize = _erodeSize;

				const int MaxDilateErodeSizePerPass = 4;

				int maxPasses = Mathf.CeilToInt(Mathf.Max(_erodeSize, _dilateSize) / (float)MaxDilateErodeSizePerPass);

				for (int i = 0; i < maxPasses; i++)
				{
					float dilateSize2 = Mathf.Min(dilateSize, MaxDilateErodeSizePerPass);
					float erodeSize2 = Mathf.Min(erodeSize, MaxDilateErodeSizePerPass);

					RenderTexture dst = _rtResult;
					if (source == _rtResult)
					{
						dst = _rtResult2;
					}
					DoPass(dilateSize2, erodeSize2, source, dst);
					source = dst;

					dilateSize = Mathf.Max(0f, dilateSize - MaxDilateErodeSizePerPass);
					erodeSize = Mathf.Max(0f, erodeSize - MaxDilateErodeSizePerPass);
				}
			}
			else
			{
				DoPass(_dilateSize, _erodeSize, source, _rtResult);
			}

			// Free intermediate textures
			RenderTextureHelper.ReleaseTemporary(ref rtAlphaSource);

			RenderTexture.active = prevRT;

			return _output;
		}

		private void DoPass(float dilateSize, float erodeSize, RenderTexture src, RenderTexture dst)
		{
			_material.SetFloat(ShaderProp.ErodeRadius, erodeSize);
			_material.SetFloat(ShaderProp.DilateRadius, dilateSize);

			dst.DiscardContents();

			if (erodeSize <= 0f)
			{
				// Do only dilate
				if (dilateSize > 0f)
				{
					Graphics.Blit(src, dst, _material, _alphaOnly ? ShaderPass.DilateAlpha : ShaderPass.Dilate);
					dst.IncrementUpdateCount();
					_output = dst;
				}
				// Do nothing, just copy
				else
				{
					if (_alphaOnly)
					{
						Graphics.Blit(src, dst, _material, ShaderPass.CopyAlpha);
					}
					else
					{
						Graphics.Blit(src, dst);
					}
					dst.IncrementUpdateCount();
					_output = dst;
				}
			}
			else
			{
				// Do both erode and dilate
				if (dilateSize > 0f)
				{
					Graphics.Blit(src, dst, _material, ShaderPass.ErodeDilateAlpha);
					dst.IncrementUpdateCount();
					_output = dst;
				}
				else
				// Do only erode
				{
					Graphics.Blit(src, dst, _material, _alphaOnly ? ShaderPass.ErodeAlpha : ShaderPass.Erode);
					dst.IncrementUpdateCount();
					_output = dst;
				} 
			}
		}

		public void FreeResources()
		{
			FreeShaders();
			FreeTextures();
		}

		private uint _currentTextureHash;

		private static uint CreateTextureHash(int width, int height, bool alphaOnly, bool useMultiPassOptimisation)
		{
			uint hash = 0;
			hash = (hash << 0) | (uint)width;
			hash = (hash << 13) | (uint)height;
			hash = (hash << 1) | (uint)(alphaOnly?1:0);
			hash = (hash << 1) | (uint)(useMultiPassOptimisation?1:0);
			return hash;
		}

		void SetupResources(RenderTexture sourceTexture)
		{
			uint desiredTextureHash = 0;
			if (sourceTexture != null)
			{
				desiredTextureHash = CreateTextureHash(sourceTexture.width, sourceTexture.height, _alphaOnly, _useMultiPassOptimisation);
			}

			if (desiredTextureHash != _currentTextureHash)
			{
				FreeTextures();
				_materialsDirty = true;
			}

			if (_sourceTexture == null && sourceTexture != null)
			{
				CreateTextures(sourceTexture);
				_currentTextureHash = desiredTextureHash;
			}

			if (_sourceTexture != sourceTexture)
			{
				_materialsDirty = true;
				_sourceTexture = sourceTexture;
			}
			if (_material == null)
			{
				CreateShaders();
			}
		}

		void UpdateMaterials()
		{
			_material.SetFloat(ShaderProp.ErodeRadius, _erodeSize);
			_material.SetFloat(ShaderProp.DilateRadius, _dilateSize);
			if (_distanceShape == DistanceShape.Square)
			{
				_material.DisableKeyword(ShaderKeyword.DistDiamond);
				_material.DisableKeyword(ShaderKeyword.DistCircle);
				_material.EnableKeyword(ShaderKeyword.DistSquare);
			}
			else if (_distanceShape == DistanceShape.Diamond)
			{
				_material.DisableKeyword(ShaderKeyword.DistSquare);
				_material.DisableKeyword(ShaderKeyword.DistCircle);
				_material.EnableKeyword(ShaderKeyword.DistDiamond);
			}
			else if (_distanceShape == DistanceShape.Circle)
			{
				_material.DisableKeyword(ShaderKeyword.DistSquare);
				_material.DisableKeyword(ShaderKeyword.DistDiamond);
				_material.EnableKeyword(ShaderKeyword.DistCircle);
			}
			_materialsDirty = false;
		}

		static Material CreateMaterialFromShader(string shaderName)
		{
			Material result = null;
			Shader shader = Shader.Find(shaderName);
			if (shader != null)
			{
				result = new Material(shader);
				result.hideFlags = HideFlags.DontSave;
			}
			return result;
		}

		void CreateShaders()
		{
			_material = CreateMaterialFromShader(ShaderId);
			Debug.Assert(_material != null);
			_materialsDirty = true;
		}

		void CreateTextures(RenderTexture sourceTexture)
		{
			Debug.Assert(sourceTexture.width > 0 && sourceTexture.height > 0);
			int w = sourceTexture.width / 1;
			int h = sourceTexture.height / 1;

			RenderTextureFormat format = RenderTextureFormat.ARGBHalf;
			if ((Filters.PerfHint & PerformanceHint.UseLessPrecision) != 0)
			{
				format = RenderTextureFormat.ARGB32;
			}
			if (_alphaOnly)
			{
				format = RenderTextureFormat.RHalf;
				if ((Filters.PerfHint & PerformanceHint.UseLessPrecision) != 0)
				{
					format = RenderTextureFormat.R8;
				}
			}

			_rtResult = RenderTexture.GetTemporary(w, h, 0, format, RenderTextureReadWrite.Linear);
			_rtResult.SetDebugName("ErodeDilate");
			if (_useMultiPassOptimisation)
			{
				_rtResult2 = RenderTexture.GetTemporary(w, h, 0, format, RenderTextureReadWrite.Linear);
				_rtResult2.SetDebugName("ErodeDilate-Multi");
			}
		}

		void FreeShaders()
		{
			ObjectHelper.Destroy(ref _material);
		}

		public void FreeTextures()
		{
			RenderTextureHelper.ReleaseTemporary(ref _rtResult2);
			RenderTextureHelper.ReleaseTemporary(ref _rtResult);
			_currentTextureHash = 0;
			_sourceTexture = null;
			_output = null;
		}
	}
}