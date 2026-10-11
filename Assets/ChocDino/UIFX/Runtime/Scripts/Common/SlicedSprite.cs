#if true
//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;

namespace ChocDino.UIFX
{
	internal static class SlicedSprite
	{
		/// <summary>
		/// Generate vertices for a 9-sliced Image.
		/// </summary>

		static readonly Vector2[] s_VertScratch = new Vector2[4];
		static readonly Vector2[] s_UVScratch = new Vector2[4];

		internal static void Generate9SliceGeometry_Stretch(Sprite overrideSprite, Rect rect, bool fillCenter, Color color, float pixelsPerUnit, VertexHelper toFill)
		{
			//bool hasBorder = (overrideSprite != null && overrideSprite.sqrMagnitude > 0f);

			/*if (!hasBorder)
			{
				GenerateSimpleSprite(toFill, false);
				return;
			}*/

			Vector4 outer, inner, padding, border;

			if (overrideSprite != null)
			{
				outer = DataUtility.GetOuterUV(overrideSprite);
				inner = DataUtility.GetInnerUV(overrideSprite);
				padding = DataUtility.GetPadding(overrideSprite);
				border = overrideSprite.border;
			}
			else
			{
				outer = Vector4.zero;
				inner = Vector4.zero;
				padding = Vector4.zero;
				border = Vector4.zero;
			}

			border = GetAdjustedBorders(border / pixelsPerUnit, rect);
			padding = padding / pixelsPerUnit;

			s_VertScratch[0] = new Vector2(padding.x, padding.y);
			s_VertScratch[3] = new Vector2(rect.width - padding.z, rect.height - padding.w);

			s_VertScratch[1].x = border.x;
			s_VertScratch[1].y = border.y;
			s_VertScratch[2].x = rect.width - border.z;
			s_VertScratch[2].y = rect.height - border.w;

			for (int i = 0; i < 4; ++i)
			{
				s_VertScratch[i].x += rect.x;
				s_VertScratch[i].y += rect.y;
			}

			s_UVScratch[0] = new Vector2(outer.x, outer.y);
			s_UVScratch[1] = new Vector2(inner.x, inner.y);
			s_UVScratch[2] = new Vector2(inner.z, inner.w);
			s_UVScratch[3] = new Vector2(outer.z, outer.w);

			toFill.Clear();

			for (int x = 0; x < 3; ++x)
			{
				int x2 = x + 1;

				for (int y = 0; y < 3; ++y)
				{
					if (!fillCenter && x == 1 && y == 1)
						continue;

					int y2 = y + 1;

					AddQuad(toFill,
						new Vector2(s_VertScratch[x].x, s_VertScratch[y].y),
						new Vector2(s_VertScratch[x2].x, s_VertScratch[y2].y),
						color,
						new Vector2(s_UVScratch[x].x, s_UVScratch[y].y),
						new Vector2(s_UVScratch[x2].x, s_UVScratch[y2].y));
				}
			}
		}


        internal static void Generate9SliceGeometry_Tile(Sprite overrideSprite, Rect rect, bool fillCenter, Color color, float pixelsPerUnit, VertexHelper toFill)
        {
            Vector4 outer, inner, border;
            Vector2 spriteSize;

            if (overrideSprite != null)
            {
                outer = DataUtility.GetOuterUV(overrideSprite);
                inner = DataUtility.GetInnerUV(overrideSprite);
                border = overrideSprite.border;
                spriteSize = overrideSprite.rect.size;
            }
            else
            {
                outer = Vector4.zero;
                inner = Vector4.zero;
                border = Vector4.zero;
                spriteSize = Vector2.one * 100;
            }

            //Rect rect = GetPixelAdjustedRect();
            float tileWidth = (spriteSize.x - border.x - border.z) / pixelsPerUnit;
            float tileHeight = (spriteSize.y - border.y - border.w) / pixelsPerUnit;
            border = GetAdjustedBorders(border / pixelsPerUnit, rect);

            var uvMin = new Vector2(inner.x, inner.y);
            var uvMax = new Vector2(inner.z, inner.w);

            var v = UIVertex.simpleVert;
            v.color = color;

            // Min to max max range for tiled region in coordinates relative to lower left corner.
            float xMin = border.x;
            float xMax = rect.width - border.z;
            float yMin = border.y;
            float yMax = rect.height - border.w;

            toFill.Clear();
            var clipped = uvMax;

            // if either with is zero we cant tile so just assume it was the full width.
            if (tileWidth == 0)
                tileWidth = xMax - xMin;

            if (tileHeight == 0)
                tileHeight = yMax - yMin;

            if (fillCenter)
            {
                for (float y1 = yMin; y1 < yMax; y1 += tileHeight)
                {
                    float y2 = y1 + tileHeight;
                    if (y2 > yMax)
                    {
                        clipped.y = uvMin.y + (uvMax.y - uvMin.y) * (yMax - y1) / (y2 - y1);
                        y2 = yMax;
                    }

                    clipped.x = uvMax.x;
                    for (float x1 = xMin; x1 < xMax; x1 += tileWidth)
                    {
                        float x2 = x1 + tileWidth;
                        if (x2 > xMax)
                        {
                            clipped.x = uvMin.x + (uvMax.x - uvMin.x) * (xMax - x1) / (x2 - x1);
                            x2 = xMax;
                        }
                        AddQuad(toFill, new Vector2(x1, y1) + rect.position, new Vector2(x2, y2) + rect.position, color, uvMin, clipped);
                    }
                }
            }

			bool hasBorder = (overrideSprite != null && overrideSprite.border.sqrMagnitude > 0f);

            if (hasBorder)
            {
                clipped = uvMax;
                for (float y1 = yMin; y1 < yMax; y1 += tileHeight)
                {
                    float y2 = y1 + tileHeight;
                    if (y2 > yMax)
                    {
                        clipped.y = uvMin.y + (uvMax.y - uvMin.y) * (yMax - y1) / (y2 - y1);
                        y2 = yMax;
                    }
                    AddQuad(toFill,
                        new Vector2(0, y1) + rect.position,
                        new Vector2(xMin, y2) + rect.position,
                        color,
                        new Vector2(outer.x, uvMin.y),
                        new Vector2(uvMin.x, clipped.y));
                    AddQuad(toFill,
                        new Vector2(xMax, y1) + rect.position,
                        new Vector2(rect.width, y2) + rect.position,
                        color,
                        new Vector2(uvMax.x, uvMin.y),
                        new Vector2(outer.z, clipped.y));
                }

                // Bottom and top tiled border
                clipped = uvMax;
                for (float x1 = xMin; x1 < xMax; x1 += tileWidth)
                {
                    float x2 = x1 + tileWidth;
                    if (x2 > xMax)
                    {
                        clipped.x = uvMin.x + (uvMax.x - uvMin.x) * (xMax - x1) / (x2 - x1);
                        x2 = xMax;
                    }
                    AddQuad(toFill,
                        new Vector2(x1, 0) + rect.position,
                        new Vector2(x2, yMin) + rect.position,
                        color,
                        new Vector2(uvMin.x, outer.y),
                        new Vector2(clipped.x, uvMin.y));
                    AddQuad(toFill,
                        new Vector2(x1, yMax) + rect.position,
                        new Vector2(x2, rect.height) + rect.position,
                        color,
                        new Vector2(uvMin.x, uvMax.y),
                        new Vector2(clipped.x, outer.w));
                }

                // Corners
                AddQuad(toFill,
                    new Vector2(0, 0) + rect.position,
                    new Vector2(xMin, yMin) + rect.position,
                    color,
                    new Vector2(outer.x, outer.y),
                    new Vector2(uvMin.x, uvMin.y));
                AddQuad(toFill,
                    new Vector2(xMax, 0) + rect.position,
                    new Vector2(rect.width, yMin) + rect.position,
                    color,
                    new Vector2(uvMax.x, outer.y),
                    new Vector2(outer.z, uvMin.y));
                AddQuad(toFill,
                    new Vector2(0, yMax) + rect.position,
                    new Vector2(xMin, rect.height) + rect.position,
                    color,
                    new Vector2(outer.x, uvMax.y),
                    new Vector2(uvMin.x, outer.w));
                AddQuad(toFill,
                    new Vector2(xMax, yMax) + rect.position,
                    new Vector2(rect.width, rect.height) + rect.position,
                    color,
                    new Vector2(uvMax.x, uvMax.y),
                    new Vector2(outer.z, outer.w));
            }
        }		

        static Vector4 GetAdjustedBorders(Vector4 border, Rect rect)
        {
            for (int axis = 0; axis <= 1; axis++)
            {
                // If the rect is smaller than the combined borders, then there's not room for the borders at their normal size.
                // In order to avoid artefacts with overlapping borders, we scale the borders down to fit.
                float combinedBorders = border[axis] + border[axis + 2];
                if (rect.size[axis] < combinedBorders && combinedBorders != 0)
                {
                    float borderScaleRatio = rect.size[axis] / combinedBorders;
                    border[axis] *= borderScaleRatio;
                    border[axis + 2] *= borderScaleRatio;
                }
            }
            return border;
        }

        static void AddQuad(VertexHelper vertexHelper, Vector2 posMin, Vector2 posMax, Color32 color, Vector2 uvMin, Vector2 uvMax)
        {
            int startIndex = vertexHelper.currentVertCount;

            vertexHelper.AddVert(new Vector3(posMin.x, posMin.y, 0), color, new Vector2(uvMin.x, uvMin.y));
            vertexHelper.AddVert(new Vector3(posMin.x, posMax.y, 0), color, new Vector2(uvMin.x, uvMax.y));
            vertexHelper.AddVert(new Vector3(posMax.x, posMax.y, 0), color, new Vector2(uvMax.x, uvMax.y));
            vertexHelper.AddVert(new Vector3(posMax.x, posMin.y, 0), color, new Vector2(uvMax.x, uvMin.y));

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }		
	}
}
#endif