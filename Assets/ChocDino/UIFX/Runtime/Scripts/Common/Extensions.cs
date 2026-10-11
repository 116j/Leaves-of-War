//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

#if UNITY_2020_1_OR_NEWER
	#define UNITY_UI_PREMULTIPLIED
#endif
#if UIFX_DEBUG && (UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_INCLUDE_INSTRUMENTATION)
	#define UIFX_DEBUG_NAMES
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityBlendMode = UnityEngine.Rendering.BlendMode;

namespace ChocDino.UIFX
{
	public static class Matrix4x4Helper
	{
		public static Matrix4x4 Rotate(Quaternion rotation)
		{
			#if UNITY_2017_1_OR_NEWER
			return Matrix4x4.Rotate(rotation);
			#else
			return Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);
			#endif
		}
	}

	public static class ObjectHelper
	{
		[System.Diagnostics.Conditional("UIFX_DEBUG_NAMES")]
		internal static void SetDebugName(this Object obj, string name)
		{
			if (obj != null)
			{
				obj.name = name;
			}
		}

		[System.Diagnostics.Conditional("UIFX_DEBUG_NAMES")]
		internal static void SetDebugName(this Object obj, string name, System.Func<string> extraProvider)
		{
			if (obj != null && extraProvider != null)
			{
				string extra = extraProvider();
				obj.name = $"{name} - {extra}";
			}
		}

		public static void Destroy<T>(ref T obj, bool delayDestroy = false) where T : Object
		{
			if (obj)
			{
				Destroy(obj, delayDestroy);
				obj = null;
			}
		}

		public static void Destroy<T>(T obj, bool delayDestroy = false) where T : Object
		{
			if (obj != null)
			{
				#if UNITY_EDITOR
				if (delayDestroy)
				{
					UnityEditor.EditorApplication.delayCall += () =>
					{
						Object.DestroyImmediate(obj);
					};
					return;
				}
				#endif
				
				#if UNITY_EDITOR
				if (ShouldUseDestroyImmediate())
				{
					Object.DestroyImmediate(obj);
					return;
				}
				#endif

				Debug.Assert(delayDestroy == false);
				Object.Destroy(obj);
			}
		}

		#if UNITY_EDITOR
		static bool ShouldUseDestroyImmediate()
		{
			return !Application.isPlaying || 
				UnityEditor.BuildPipeline.isBuildingPlayer ||
				UnityEditor.EditorApplication.isCompiling ||
				UnityEditor.EditorApplication.isUpdating;
		}
		#endif
		
		public static void Dispose<T>(ref T obj) where T : System.IDisposable
		{
			if (obj != null)
			{
				obj.Dispose();
				obj = default(T);
			}
		}

		public static bool ChangeProperty<T>(ref T backing, T value) where T : struct
		{
			if (typeof(T).IsEnum || typeof(T) == typeof(bool))
			{
				Debug.Assert(false);
			}

			// NOTE: This generates garbage for Enums and bools.
			if (!(EqualityComparer<T>.Default.Equals(backing, value)))
			{
				backing = value;
				return true;
			}
			return false;
		}

		public static bool ChangeProperty(ref bool backing, bool value)
		{
			if (backing != value)
			{
				backing = value;
				return true;
			}
			return false;
		}

		public static bool ChangePropertyEnum<T>(ref T backing, T value) where T : struct, System.Enum
		{
			if (backing.GetHashCode() != value.GetHashCode())
			{
				backing = value;
				return true;
			}
			return false;
		}
	}

	public static class RenderTextureHelper
	{
		public static void ReleaseTemporary(ref RenderTexture rt)
		{
			if (rt)
			{
				RenderTexture.ReleaseTemporary(rt); rt = null;
			}
		}
	}

	public static class VertexHelperExtensions
	{
		public static void ReplaceUIVertexTriangleStream(this VertexHelper vh, List<UIVertex> vertices)
		{
			// NOTE: despite its name, this method actually replaces the vertices, it doesn't add to them
			vh.AddUIVertexTriangleStream(vertices);
		}
	}

	public static class MaterialHelper
	{
		public static bool MaterialOutputsPremultipliedAlpha(Material material)
		{
			bool result = false;
			Debug.Assert(material != null);
			if (material)
			{
				// Detect use of "UI/Default" shader
				if (material == Graphic.defaultGraphicMaterial || material.shader == Graphic.defaultGraphicMaterial.shader)
				{
					#if UNITY_UI_PREMULTIPLIED
					return true;
					#else
					return false;
					#endif
				}

				if (material.HasProperty(UnityShaderProp.SrcBlend) && material.HasProperty(UnityShaderProp.DstBlend))
				{
					result = ((material.GetInt(UnityShaderProp.SrcBlend) == (int)UnityBlendMode.One) && (material.GetInt(UnityShaderProp.DstBlend) == (int)UnityBlendMode.OneMinusSrcAlpha));
					if (result)
					{
						// If there is also alpha-channel blend factors then see if they also correspond to premultipled alpha.
						if (material.HasProperty(UnityShaderProp.SrcBlendAlpha) && material.HasProperty(UnityShaderProp.DstBlendAlpha))
						{
							result = ((material.GetInt(UnityShaderProp.SrcBlendAlpha) == (int)UnityBlendMode.One) && (material.GetInt(UnityShaderProp.DstBlendAlpha) == (int)UnityBlendMode.OneMinusSrcAlpha));
							return result;
						}
						return result;
					}
				}

				// This is a alternative blend mode property name that we have encountered
				if (material.HasProperty(UnityShaderProp.BlendSrc) && material.HasProperty(UnityShaderProp.BlendDst))
				{
					result = ((material.GetInt(UnityShaderProp.BlendSrc) == (int)UnityBlendMode.One) && (material.GetInt(UnityShaderProp.BlendDst) == (int)UnityBlendMode.OneMinusSrcAlpha));
					if (result)
					{
						// If there is also alpha-channel blend factors then see if they also correspond to premultipled alpha.
						if (material.HasProperty(UnityShaderProp.AlphaSrcBlend) && material.HasProperty(UnityShaderProp.AlphaDstBlend))
						{
							result = ((material.GetInt(UnityShaderProp.AlphaSrcBlend) == (int)UnityBlendMode.One) && (material.GetInt(UnityShaderProp.AlphaDstBlend) == (int)UnityBlendMode.OneMinusSrcAlpha));
							return result;
						}
						return result;
					}
				}

				string tag = material.GetTag("OutputsPremultipliedAlpha", false, string.Empty);
				if (!string.IsNullOrEmpty(tag))
				{
					return string.Equals(tag, "true", System.StringComparison.OrdinalIgnoreCase);
				}
			}

			// Assume the output is pre-multiplied if Unity version > 2020
			// But we can't assume this for non-unity shaders so we're just guessing at this stage..
			#if UNITY_UI_PREMULTIPLIED
			result = true;
			#endif

			return result;
		}
	}

	public static class AnimationCurveHelper
	{
		public static bool HasOutOfRangeValues(this AnimationCurve curve, float timeMin, float timeMax, float valueMin, float valueMax = float.MaxValue)
		{
			bool result = false;
			foreach (var key in curve.keys)
			{
				if (key.time < timeMin || key.time > timeMax || key.value < valueMin || key.value > valueMax)
				{
					result = true;
					break;
				}
			}
			return result;
		}
	}

	public static class EditorHelper
	{
		#if UNITY_EDITOR
		public static bool IsEditingCurve()
		{
			return IsInteractingWithWindow("CurveEditorWindow");
		}

		private static bool IsInteractingWithWindow(string windowName)
		{
			bool result = false;
			var window = UnityEditor.EditorWindow.focusedWindow;
			if (window)
			{
				result = window.GetType().ToString().Contains(windowName);
			}
			if (!result)
			{
				window = UnityEditor.EditorWindow.mouseOverWindow;
				if (window)
				{
					result = window.GetType().ToString().Contains(windowName);
				}
			}
			return result;
		}
		#endif

		public static bool IsInContextPrefabMode()
		{
			bool result = false;
			#if UNITY_EDITOR
			#if UNITY_2021_2_OR_NEWER
			var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null && stage.mode == UnityEditor.SceneManagement.PrefabStage.Mode.InContext)
			{
				result = true;
			}
			#elif UNITY_2020_1_OR_NEWER
			var stage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null && stage.mode == UnityEditor.Experimental.SceneManagement.PrefabStage.Mode.InContext)
			{
				result = true;
			}
			#endif
			#endif
			return result;
		}
		
		public static bool IsPartOfPrefabStage(GameObject go)
		{
			bool result = false;
			#if UNITY_EDITOR
			#if UNITY_2021_2_OR_NEWER
			var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null && stage.mode == UnityEditor.SceneManagement.PrefabStage.Mode.InContext)
			{
				result = stage.IsPartOfPrefabContents(go);
			}
			#elif UNITY_2019_1_OR_NEWER
			Debug.Log("IsPartOfPrefabStage1");
			var stage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null)
			{
				Debug.Log("IsPartOfPrefabStage2");
				result = stage.IsPartOfPrefabContents(go);
				Debug.Log("IsPartOfPrefabStage3" + result);
			}
			#endif
			#endif
			return result;
		}
	}
}