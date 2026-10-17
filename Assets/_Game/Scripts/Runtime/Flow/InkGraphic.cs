using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    [RequireComponent(typeof(CanvasRenderer))]
    public abstract class InkGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] protected float wear;
        [SerializeField] protected int seed = 7;

        protected VertexHelper vh;
        protected System.Random random;

        public float Wear
        {
            get => wear;
            set { wear = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        public int Seed
        {
            get => seed;
            set { seed = value; SetVerticesDirty(); }
        }

        protected abstract void Draw(Rect r);

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            vh = helper;
            random = new System.Random(seed);
            Draw(GetPixelAdjustedRect());
            vh = null;
        }

        protected List<Vector2> Arc(Vector2 c, float rx, float ry, float from, float to, int steps)
        {
            var points = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                points.Add(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
            }
            return points;
        }

        protected void Box(Vector2 c, float hx, float hy, float width, float keep)
        {
            Stroke(new List<Vector2>
            {
                c + new Vector2(-hx, -hy), c + new Vector2(hx, -hy), c + new Vector2(hx, hy), c + new Vector2(-hx, hy), c + new Vector2(-hx, -hy)
            }, width, keep);
        }

        protected void Stroke(List<Vector2> points, float width, float keep)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                float roll = (float)random.NextDouble();
                if (roll > keep + (1f - wear) * (1f - keep))
                    continue;
                float alpha = Mathf.Lerp(1f, 0.45f + 0.55f * (float)random.NextDouble(), wear);
                Vector2 a = points[i], b = points[i + 1];
                Vector2 d = b - a;
                if (d.sqrMagnitude < 1e-6f)
                    continue;
                Vector2 n = new Vector2(-d.y, d.x).normalized * width * 0.5f;
                Vector2 e = d.normalized * width * 0.35f;
                Quad(a - e + n, b + e + n, b + e - n, a - e - n, alpha);
            }
        }

        protected void Leaf(Vector2 start, Vector2 dir, float length, float width)
        {
            Vector2 n = new Vector2(-dir.y, dir.x);
            var outline = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                outline.Add(start + dir * length * t + n * Mathf.Sin(t * Mathf.PI) * width * 0.5f);
            }
            for (int i = 9; i >= 1; i--)
            {
                float t = i / 10f;
                outline.Add(start + dir * length * t - n * Mathf.Sin(t * Mathf.PI) * width * 0.5f);
            }
            Fan(start + dir * length * 0.5f, outline, 1f);
        }

        protected void Diamond(Vector2 c, float hx, float hy, bool filled)
        {
            var points = new List<Vector2> { c + new Vector2(0f, hy), c + new Vector2(hx, 0f), c + new Vector2(0f, -hy), c + new Vector2(-hx, 0f) };
            if (filled)
            {
                Fan(c, points, 1f);
                return;
            }
            points.Add(points[0]);
            Stroke(points, 1.8f, 1f);
        }

        protected void Star(Vector2 c, float radius)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + i * 36f) * Mathf.Deg2Rad;
                float rr = i % 2 == 0 ? radius : radius * 0.42f;
                points.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
            Fan(c, points, Mathf.Lerp(1f, 0.6f, wear));
        }

        protected void Disc(Vector2 c, float radius, Color fill)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                points.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            Color keep = color;
            color = fill;
            Fan(c, points, 1f);
            color = keep;
        }

        protected void Fan(Vector2 centre, List<Vector2> outline, float alpha)
        {
            int start = vh.currentVertCount;
            vh.AddVert(Vertex(centre, alpha));
            foreach (Vector2 p in outline)
                vh.AddVert(Vertex(p, alpha));
            for (int i = 0; i < outline.Count; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % outline.Count);
        }

        protected void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float alpha)
        {
            int start = vh.currentVertCount;
            vh.AddVert(Vertex(a, alpha));
            vh.AddVert(Vertex(b, alpha));
            vh.AddVert(Vertex(c, alpha));
            vh.AddVert(Vertex(d, alpha));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        protected UIVertex Vertex(Vector2 p, float alpha)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = p;
            Color c = color;
            c.a *= alpha;
            v.color = c;
            return v;
        }
    }
}
