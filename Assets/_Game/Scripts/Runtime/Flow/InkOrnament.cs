using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkOrnament : InkGraphic
    {
        public enum Kind { None, LibertyLine, HotelStamp, MilitaryPost, DecoLine, TypedDate, FieldPost }

        [SerializeField] private Kind kind;

        public Kind Shape
        {
            get => kind;
            set { kind = value; SetVerticesDirty(); }
        }

        protected override void Draw(Rect r)
        {
            switch (kind)
            {
                case Kind.LibertyLine: Liberty(r); break;
                case Kind.DecoLine: Deco(r); break;
                case Kind.HotelStamp: Hotel(r); break;
                case Kind.MilitaryPost: Military(r); break;
                case Kind.TypedDate: Typed(r); break;
                case Kind.FieldPost: Field(r); break;
            }
        }

        private void Liberty(Rect r)
        {
            float w = r.width * 0.5f - 40f;
            float amp = r.height * 0.14f;
            var main = new List<Vector2>();
            for (int i = 0; i <= 120; i++)
            {
                float t = i / 120f;
                float x = Mathf.Lerp(-w, w, t);
                float taper = Mathf.Sin(t * Mathf.PI);
                main.Add(r.center + new Vector2(x, Mathf.Sin(t * Mathf.PI * 3f) * amp * (0.35f + 0.65f * taper)));
            }
            Stroke(main, 2.6f, 1.2f);
            Spiral(main[0], 1f, r.height * 0.26f);
            Spiral(main[main.Count - 1], -1f, r.height * 0.26f);
            for (int k = 1; k <= 8; k++)
            {
                if (k == 4 || k == 5)
                    continue;
                int i = k * 120 / 9;
                Vector2 a = main[i], b = main[Mathf.Min(i + 2, main.Count - 1)];
                Vector2 tangent = (b - a).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x) * (k % 2 == 0 ? 1f : -1f);
                Vector2 dir = (normal * 0.8f + tangent * (k < 5 ? -0.6f : 0.6f)).normalized;
                var stem = new List<Vector2> { a, a + dir * 10f, a + dir * 18f + tangent * 4f };
                Stroke(stem, 1.4f, 1f);
                Leaf(a + dir * 18f, dir, 22f, 7f);
            }
            Vector2 c = main[60];
            for (int p = 0; p < 5; p++)
            {
                float angle = 90f + p * 72f;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Leaf(c + dir * 4f, dir, 20f, 9f);
            }
            Disc(c, 4f, color);
            Disc(c, 2f, new Color(0.94f, 0.9f, 0.82f, color.a));
        }

        private void Spiral(Vector2 start, float side, float radius)
        {
            var points = new List<Vector2>();
            Vector2 centre = start + new Vector2(side * radius * 0.2f, radius * 0.75f);
            float a0 = Mathf.Atan2(start.y - centre.y, start.x - centre.x);
            float r0 = Vector2.Distance(start, centre);
            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                float angle = a0 + side * t * Mathf.PI * 3.2f;
                float rr = Mathf.Lerp(r0, r0 * 0.12f, t);
                points.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rr);
            }
            Stroke(points, 2.2f, 0.9f);
            Disc(points[points.Count - 1], 2.4f, color);
        }

        private void Deco(Rect r)
        {
            Vector2 c = r.center;
            float w = r.width * 0.5f - 30f;
            float[] lengths = { w, w - 40f, w - 80f };
            float[] heights = { -7f, 0f, 7f };
            for (int i = 0; i < 3; i++)
            {
                float y = heights[i];
                float inner = 70f;
                Stroke(new List<Vector2> { c + new Vector2(-lengths[i], y), c + new Vector2(-inner, y) }, i == 1 ? 2.4f : 1.4f, 1f);
                Stroke(new List<Vector2> { c + new Vector2(inner, y), c + new Vector2(lengths[i], y) }, i == 1 ? 2.4f : 1.4f, 1f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 3; k++)
                {
                    float x = s * (30f + k * 13f);
                    Stroke(new List<Vector2> { c + new Vector2(x + s * 8f, 12f), c + new Vector2(x, 0f), c + new Vector2(x + s * 8f, -12f) }, 1.8f, 1f);
                }
                Vector2 end = c + new Vector2(s * (w + 10f), 0f);
                Diamond(end, 7f, 7f, true);
                Diamond(c + new Vector2(s * (w - 120f), 0f), 4f, 9f, true);
            }
            Diamond(c, 16f, 22f, false);
            Diamond(c, 7f, 11f, true);
            var fan = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float a = Mathf.PI * i / 24f;
                fan.Add(c + new Vector2(Mathf.Cos(a) * 30f, 26f + Mathf.Sin(a) * 30f));
            }
            Stroke(fan, 1.6f, 1f);
            for (int i = 1; i < 8; i++)
            {
                float a = Mathf.PI * i / 8f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Stroke(new List<Vector2> { c + new Vector2(0f, 26f) + dir * 9f, c + new Vector2(0f, 26f) + dir * 28f }, 1.2f, 1f);
            }
            Stroke(Arc(c + new Vector2(0f, -26f), 30f, 30f, 180f, 360f, 24), 1.6f, 1f);
        }

        private void Hotel(Rect r)
        {
            Vector2 c = r.center;
            float rx = r.width * 0.5f - 6f, ry = r.height * 0.5f - 6f;
            Stroke(Arc(c, rx, ry, 0f, 360f, 96), 3f, 0.75f);
            Stroke(Arc(c, rx - 10f, ry - 10f, 0f, 360f, 96), 1.4f, 0.75f);
            Stroke(Arc(c, rx - 46f, ry - 40f, 0f, 360f, 80), 1.2f, 0.75f);
            foreach (float s in new[] { -1f, 1f })
                Star(c + new Vector2(s * (rx - 26f), 0f), 6f);
        }

        private void Military(Rect r)
        {
            Vector2 c = r.center;
            float rad = Mathf.Min(r.width, r.height) * 0.5f - 6f;
            Stroke(Arc(c, rad, rad, 0f, 360f, 96), 3.2f, 0.7f);
            Stroke(Arc(c, rad - 38f, rad - 38f, 0f, 360f, 80), 1.6f, 0.7f);
            foreach (float y in new[] { 20f, -20f })
                Stroke(new List<Vector2> { c + new Vector2(-rad + 42f, y), c + new Vector2(rad - 42f, y) }, 1.4f, 0.7f);
            Star(c + new Vector2(-rad + 20f, 0f), 5f);
            Star(c + new Vector2(rad - 20f, 0f), 5f);
        }

        private void Typed(Rect r)
        {
            Vector2 c = r.center;
            float hx = r.width * 0.5f - 4f, hy = r.height * 0.5f - 4f;
            Box(c, hx, hy, 2.6f, 0.8f);
            Box(c, hx - 7f, hy - 7f, 1.2f, 0.8f);
            Stroke(new List<Vector2> { c + new Vector2(-hx + 12f, hy * 0.18f), c + new Vector2(hx - 12f, hy * 0.18f) }, 1f, 0.8f);
        }

        private void Field(Rect r)
        {
            Vector2 c = r.center;
            float rad = Mathf.Min(r.width, r.height) * 0.5f - 6f;
            Stroke(Arc(c, rad, rad, 0f, 360f, 110), 2.8f, 0.45f);
            Stroke(Arc(c, rad - 36f, rad - 36f, 200f, 520f, 90), 1.3f, 0.45f);
            Stroke(new List<Vector2> { c + new Vector2(-rad + 44f, -16f), c + new Vector2(rad - 44f, -16f) }, 1.2f, 0.45f);
        }

    }
}
