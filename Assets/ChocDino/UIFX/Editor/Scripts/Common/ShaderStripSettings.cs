//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	//[CreateAssetMenu(fileName = "UIFX-Shader-Strip-Settings", menuName = "UIFX/Shader Strip Settings", order = 1)]
	public class ShaderStripSettings : ScriptableObject
	{
		private const string SettingsFilename = "UIFX-Shader-Strip-Settings.asset";
		//#pragma warning disable 0414 // "field is assigned but its value is never used"
		[SerializeField] bool _enabled = false;
		[SerializeField] string[] _shaderStripEffectNames = new string[0];
		//#pragma warning restore 0414

		public bool Enabled { get => _enabled; }
		public string[] ShaderStripEffectNames { get => _shaderStripEffectNames; }

		internal static ShaderStripSettings CreateDefaultAsset()
		{
			ShaderStripSettings result = null;
			string[] assets = AssetDatabase.FindAssets("ChocDino.UIFX.Editor");
			if (assets != null && assets.Length > 0)
			{
				// By default place it in the UIFX Editor folder.
				var settingsPath = AssetDatabase.GUIDToAssetPath(assets[0]);
				settingsPath = System.IO.Path.GetDirectoryName(settingsPath);
				settingsPath = System.IO.Path.Combine(settingsPath, SettingsFilename);

				result = ScriptableObject.CreateInstance<ShaderStripSettings>();
				if (result != null)
				{
					result.hideFlags = HideFlags.DontSaveInBuild;
					AssetDatabase.CreateAsset(result, settingsPath);
					AssetDatabase.SaveAssets();
				}
			}
			return result;
		}

		internal static ShaderStripSettings FindAsset()
		{
			ShaderStripSettings result = null;
			string[] settings = AssetDatabase.FindAssets($"t:{nameof(ShaderStripSettings)}");
			if (settings != null && settings.Length > 0)
			{
				var settingsPath = AssetDatabase.GUIDToAssetPath(settings[0]);
				result = AssetDatabase.LoadAssetAtPath<ShaderStripSettings>(settingsPath);
				if (settings.Length > 1)
				{
					Debug.LogError("[UIFX] Found multiple ShaderStripSettings files - there should be only one.");
				}
			}
			return result;
		}

		internal List<string> GetShadersToKeep()
		{
			List<string> shadersToKeep = new List<string>(64);

			{
				// Collect ShaderUsages that aren't stripped.
				List<ShaderUsage> notStripped = new List<ShaderUsage>(64);
				foreach (var shaderUsage in ShaderUsageRegistry.All)
				{
					if (!_shaderStripEffectNames.Contains(shaderUsage.Id))
					{
						notStripped.Add(shaderUsage);
					}
				}

				// Add all the shaders to keep (including duplicates).
				foreach (var shaderUsage in notStripped)
				{
					shadersToKeep.AddRange(shaderUsage.Shaders);
				}
			}

			// Remove duplicates.
			shadersToKeep = shadersToKeep.Distinct().ToList();

			return shadersToKeep;
		}
	}
}