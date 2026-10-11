//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChocDino.UIFX.Demos
{
	internal class AskForReview : MonoBehaviour, IPointerClickHandler 
	{
		enum Product
		{
			Unknown,
			StarterBundle,
			IndieBundle,
			ProBundle,
		}

		[SerializeField] Product _product = Product.Unknown;

		public void OnPointerClick(PointerEventData eventData) 
		{
			string url = GetBestReviewUrl();
			if (!string.IsNullOrEmpty(url))
			{
				Application.OpenURL(url);
			}
		}

		private readonly static string StarterBundleId = "351962";
		private readonly static string IndieBundleId = "351964";
		private readonly static string ProBundleId = "384538";

		private static string GetReviewUrl(string assetId)
		{
			return string.Format("https://assetstore.unity.com/packages/slug/{0}?aid=1100lSvNe#reviews", assetId);
		}

		private string GetBestReviewUrl()
		{
			switch (_product)
			{
				case Product.StarterBundle:
					return GetReviewUrl(StarterBundleId);
				case Product.IndieBundle:
					return GetReviewUrl(IndieBundleId);
				case Product.ProBundle:
					return GetReviewUrl(ProBundleId);
				default:
				case Product.Unknown:
				break;
			}

			if (GetTypeFromName("ChocDino.UIFX.WipeFilter") != null)
			{
				return GetReviewUrl(ProBundleId);
			}
			if (GetTypeFromName("ChocDino.UIFX.CompositeLayer") != null)
			{
				return GetReviewUrl(IndieBundleId);
			}

			return GetReviewUrl(StarterBundleId);
		}

		public static System.Type GetTypeFromName(string name)
		{
			foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies().Reverse())
			{
				var tt = assembly.GetType(name);
				if (tt != null)
				{
					return tt;
				}
			}

			return null;
		}
	}
}