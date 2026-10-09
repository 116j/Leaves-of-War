using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// A tiny, atlas-free PS1-style arrow. Generating its geometry keeps the
    /// guidance indicator independent of a TMP glyph or imported sprite.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class ObjectiveDirectionArrowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();

            Rect rect = GetPixelAdjustedRect();
            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            float shaftHalfWidth = halfWidth * 0.22f;
            float headBase = -halfHeight * 0.08f;

            AddTriangle(
                vertices,
                new Vector2(0f, halfHeight),
                new Vector2(-halfWidth, headBase),
                new Vector2(halfWidth, headBase));
            AddQuad(
                vertices,
                new Vector2(-shaftHalfWidth, headBase),
                new Vector2(shaftHalfWidth, headBase),
                new Vector2(shaftHalfWidth, -halfHeight),
                new Vector2(-shaftHalfWidth, -halfHeight));
        }

        private void AddTriangle(VertexHelper vertices, Vector2 a, Vector2 b, Vector2 c)
        {
            int start = vertices.currentVertCount;
            AddVertex(vertices, a);
            AddVertex(vertices, b);
            AddVertex(vertices, c);
            vertices.AddTriangle(start, start + 1, start + 2);
        }

        private void AddQuad(
            VertexHelper vertices,
            Vector2 lowerLeft,
            Vector2 lowerRight,
            Vector2 upperRight,
            Vector2 upperLeft)
        {
            int start = vertices.currentVertCount;
            AddVertex(vertices, lowerLeft);
            AddVertex(vertices, lowerRight);
            AddVertex(vertices, upperRight);
            AddVertex(vertices, upperLeft);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start + 2, start + 3, start);
        }

        private void AddVertex(VertexHelper vertices, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertices.AddVert(vertex);
        }
    }
}
