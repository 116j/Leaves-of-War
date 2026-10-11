//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	class TextureUtils
	{
		private static void ReadTexture(RenderTexture sourceTexture, Texture2D rwTexture)
		{
			// Assumptions
			Debug.Assert(sourceTexture != null);
			Debug.Assert(rwTexture != null);
			Debug.Assert(rwTexture.width == sourceTexture.width && rwTexture.height == sourceTexture.height);

			// Read pixels from GPU to CPU
			RenderTexture prevTexture = RenderTexture.active;
			RenderTexture.active = sourceTexture;
			rwTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0, recalculateMipMaps:false);
			rwTexture.Apply(updateMipmaps:false, makeNoLongerReadable:false);
			RenderTexture.active = prevTexture;

			rwTexture.IncrementUpdateCount();
		}

		internal static bool WriteToPNG(RenderTexture sourceTexture, Texture2D rwTexture, string outputPath)
		{
			ReadTexture(sourceTexture, rwTexture);
			byte[] data = ImageConversion.EncodeToPNG(rwTexture);
			if (data != null)
			{
				return WriteToFile(outputPath, data);
			}
			return false;
		}

		internal static bool WriteToEXR(RenderTexture sourceTexture, Texture2D rwTexture, string outputPath)
		{
			ReadTexture(sourceTexture, rwTexture);
			byte[] data = ImageConversion.EncodeToEXR(rwTexture, Texture2D.EXRFlags.CompressZIP);
			if (data != null)
			{
				return WriteToFile(outputPath, data);
			}
			return false;
		}

		private static bool WriteToFile(string filePath, byte[] data)
		{
			try
			{
				string directoryPath = System.IO.Path.GetDirectoryName(filePath);
				if (!System.IO.Directory.Exists(directoryPath))
				{
					System.IO.Directory.CreateDirectory(directoryPath);
				}
				System.IO.File.WriteAllBytes(filePath, data);
			}
			catch (System.Exception ex)
			{
				Debug.LogException(ex);
				return false;
			}
			return true;
		}
	}
}