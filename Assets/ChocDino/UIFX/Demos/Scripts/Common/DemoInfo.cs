//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX.Demos
{
	internal enum AssetBundle
	{
		None,
		Starter,
		Indie,
		Pro,
	}

	internal class DemoInfo : MonoBehaviour
	{
		public string title = string.Empty;
		public string slug = string.Empty;
		public AssetBundle bundle = AssetBundle.None;

		// If true, this is only in this exact bundle, not included in high bundles.
		public bool exactBundle = false;
	}
}