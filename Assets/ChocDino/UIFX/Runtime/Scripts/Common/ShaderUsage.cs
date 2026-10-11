//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChocDino.UIFX
{
	internal enum ShaderUsageCategory
	{
		Effects,
		Filters,
		Sources,
		Utilities,
		UIToolkitFilters,
		Other = 100,
	}

	internal partial class ShaderUsage
	{
		private string _id;
		private string _name;
		private ShaderUsageCategory _category;
		private List<string> _shaderPaths;

		public string Id { get => _id; }
		public string Name { get => _name; }
		public ShaderUsageCategory Category { get => _category; }
		public List<string> Shaders { get => _shaderPaths; }
		public int ShaderCount { get => _shaderPaths.Count; }

		private ShaderUsage() { }

		public ShaderUsage(string id, string name, ShaderUsageCategory category)
		{
			_id = id;
			_name = name;
			_category = category;
		}

		public void AddShader(string shaderPath)
		{
			if (!string.IsNullOrEmpty(shaderPath))
			{
				if (_shaderPaths == null)
				{
					_shaderPaths = new List<string>(8);
				}
				if (!_shaderPaths.Contains(shaderPath))
				{
					_shaderPaths.Add(shaderPath);
				}
			}
		}
	}

	internal class ShaderUsageRegistry
	{
		private static List<ShaderUsage> s_all = new List<ShaderUsage>(64);
		private static List<string> s_uniqueShaders;
		private static bool s_dirty = true;

		public static List<ShaderUsage> All { get { if (s_dirty) Build(); return s_all; } }
		public static List<string> UniqueShaders { get { if (s_dirty) Build(); return s_uniqueShaders; } }

		private static ShaderUsage Find(string id)
		{
			int count = s_all.Count;
			for (int i = 0; i < count; i++)
			{
				var usage = s_all[i];
				if (id == usage.Id)
				{
					return usage;
				}
			}
			return null;
		}

		public static void Register(string id, string name, ShaderUsageCategory category, string[] shadersUsed)
		{
			Debug.Assert(shadersUsed != null && shadersUsed.Length > 0);

			// NOTE: Register can be called multiple times with the same id (eg Wipe Filter).

			ShaderUsage usage = Find(id);
			if (usage == null)
			{
				usage = new ShaderUsage(id, name, category);
				s_all.Add(usage);
			}

			Debug.Assert(usage.Name == name);
			Debug.Assert(usage.Category == category);

			for (int i = 0; i < shadersUsed.Length; i++)
			{
				usage.AddShader(shadersUsed[i]);
			}

			s_dirty = true;
		}

		private static void Build()
		{
			s_all.Sort((x,y) => string.Compare(x.Name, y.Name, System.StringComparison.Ordinal));
			BuildUniqueShaders();
			s_dirty = false;
		}

		private static void BuildUniqueShaders()
		{
			s_uniqueShaders = new List<string>(64);
			foreach (var shaderUsage in s_all)
			{
				s_uniqueShaders.AddRange(shaderUsage.Shaders);
			}
			s_uniqueShaders = s_uniqueShaders.Distinct().ToList();
		}
	}
}