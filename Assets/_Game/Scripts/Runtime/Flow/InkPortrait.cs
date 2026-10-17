using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class InkPortrait : InkGraphic
    {
        public enum Sitter { Carlo, Apollinaire, Pietro, Rui, Leone, Lucien, Ungaretti }

        [SerializeField] private Sitter sitter;

        private Vector2 origin;
        private float unit;

        public static bool TryFind(string id, out Sitter found)
        {
            string key = (id ?? "").ToLowerInvariant();
            found = Sitter.Ungaretti;
            if (key.Length == 0)
                return false;
            if (key.Contains("guillaume"))
                key = "apollinaire";
            foreach (Sitter s in System.Enum.GetValues(typeof(Sitter)))
                if (key.Contains(s.ToString().ToLowerInvariant()))
                {
                    found = s;
                    return true;
                }
            return false;
        }

        public Sitter Person
        {
            get => sitter;
            set { sitter = value; SetVerticesDirty(); }
        }

        protected override void Draw(Rect r)
        {
            origin = r.center;
            unit = Mathf.Min(r.width, r.height) / 600f;
            float headWidth = sitter == Sitter.Apollinaire ? 104f : sitter == Sitter.Leone ? 84f : 92f;
            float headHeight = sitter == Sitter.Apollinaire ? 118f : 122f;
            float neck = sitter == Sitter.Leone ? 30f : sitter == Sitter.Apollinaire || sitter == Sitter.Lucien ? 44f : 38f;
            float shoulders = sitter == Sitter.Leone ? 210f : sitter == Sitter.Apollinaire ? 250f : 235f;

            Bust(shoulders, neck);
            Head(headWidth, headHeight);
            Shade(headWidth, headHeight);
            switch (sitter)
            {
                case Sitter.Carlo: Carlo(); break;
                case Sitter.Apollinaire: Apollinaire(); break;
                case Sitter.Pietro: Pietro(); break;
                case Sitter.Rui: Rui(); break;
                case Sitter.Leone: Leone(); break;
                case Sitter.Lucien: Lucien(); break;
                case Sitter.Ungaretti: Ungaretti(); break;
            }
        }

        private void Bust(float shoulders, float neck)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                Sketch(Curve(P(s * neck, -60f), P(s * (neck + 4f), -115f), P(s * (neck + 60f), -150f), P(s * (shoulders - 40f), -185f), P(s * shoulders, -260f), P(s * (shoulders + 12f), -300f)), 3.2f);
                Sketch(Curve(P(s * (neck + 2f), -118f), P(s * 18f, -175f), P(0f, -205f)), 2f);
            }
        }

        private void Head(float w, float h)
        {
            var outline = new List<Vector2>();
            for (int i = 0; i <= 40; i++)
            {
                float a = Mathf.Lerp(-80f, 262f, i / 40f) * Mathf.Deg2Rad;
                float squeeze = Mathf.Sin(a) < 0f ? 0.86f : 1f;
                outline.Add(P(Mathf.Cos(a) * w * squeeze, 40f + Mathf.Sin(a) * h));
            }
            Sketch(outline, 3f);
        }

        private void Shade(float w, float h)
        {
            for (int i = 0; i < 9; i++)
            {
                float y = 40f - h * 0.55f + i * h * 0.13f;
                float edge = w * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((y - 40f) / h, 2f))) * 0.9f;
                Sketch(new List<Vector2> { P(edge - 26f, y - 10f), P(edge - 4f, y + 12f) }, 1.3f, 1);
            }
            for (int i = 0; i < 7; i++)
            {
                float x = 40f + i * 18f;
                Sketch(new List<Vector2> { P(x, -200f - i * 4f), P(x + 22f, -170f - i * 4f) }, 1.2f, 1);
            }
        }

        private void Carlo()
        {
            Sketch(Curve(P(-30f, -6f), P(-12f, 2f), P(0f, -4f), P(12f, 2f), P(30f, -6f)), 3.4f);
            Sketch(Curve(P(-86f, 70f), P(-60f, 128f), P(0f, 152f), P(60f, 128f), P(86f, 70f)), 1.6f);
            foreach (float s in new[] { -1f, 1f })
                Sketch(Curve(P(s * 42f, -150f), P(s * 30f, -200f), P(s * 8f, -280f)), 2.4f);
            for (int i = 0; i < 3; i++)
                Disc(P(0f, -225f - i * 24f), 4f * unit, color);
            Sketch(Curve(P(-120f, -260f), P(-150f, -240f), P(-165f, -270f), P(-140f, -295f), P(-110f, -280f)), 1.8f);
            Sketch(new List<Vector2> { P(-140f, -295f), P(-150f, -320f) }, 2f);
        }

        private void Apollinaire()
        {
            Sketch(Curve(P(-30f, -110f), P(-10f, -128f), P(0f, -118f), P(10f, -128f), P(30f, -110f)), 3f);
            Diamond(P(0f, -122f), 7f * unit, 9f * unit, true);
            foreach (float s in new[] { -1f, 1f })
                Sketch(Curve(P(s * 50f, -140f), P(s * 70f, -200f), P(s * 40f, -300f)), 2.4f);
            Sketch(Curve(P(30f, -40f), P(80f, -62f), P(118f, -60f)), 2.6f);
            var bowl = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.Lerp(180f, 360f, i / 12f) * Mathf.Deg2Rad;
                bowl.Add(P(132f + Mathf.Cos(a) * 16f, -44f + Mathf.Sin(a) * 22f));
            }
            bowl.Add(P(148f, -14f));
            bowl.Add(P(116f, -14f));
            bowl.Add(bowl[0]);
            Sketch(bowl, 2.4f);
            for (int i = 0; i < 3; i++)
                Sketch(Curve(P(132f + i * 4f, 0f), P(126f + i * 6f, 26f), P(140f + i * 4f, 52f), P(130f + i * 6f, 80f)), 1.2f, 1);
        }

        private void Pietro()
        {
            Sketch(Curve(P(-98f, 110f), P(-96f, 150f), P(0f, 172f), P(96f, 150f), P(98f, 110f)), 3f);
            Sketch(new List<Vector2> { P(-100f, 108f), P(100f, 108f) }, 3f);
            Sketch(Curve(P(-60f, 108f), P(-20f, 86f), P(40f, 84f), P(110f, 96f)), 2.6f);
            Sketch(Curve(P(-30f, 120f), P(0f, 160f), P(30f, 120f)), 1.4f, 1);
            foreach (float y in new[] { -230f, -262f })
                Sketch(Curve(P(-236f, y), P(-200f, y - 10f), P(-172f, y - 16f)), 2.6f);
            Sketch(new List<Vector2> { P(-176f, -246f), P(-200f, -238f) }, 1.4f, 1);
            Lantern(P(170f, -220f));
        }

        private void Lantern(Vector2 c)
        {
            float u = unit;
            var frame = new List<Vector2> { c + new Vector2(-24f, -40f) * u, c + new Vector2(24f, -40f) * u, c + new Vector2(18f, 30f) * u, c + new Vector2(-18f, 30f) * u, c + new Vector2(-24f, -40f) * u };
            Sketch(frame, 2.6f);
            Sketch(Curve(c + new Vector2(-14f, 30f) * u, c + new Vector2(0f, 64f) * u, c + new Vector2(14f, 30f) * u), 2f);
            Sketch(new List<Vector2> { c + new Vector2(-28f, -40f) * u, c + new Vector2(28f, -40f) * u }, 3.2f);
            Leaf(c + new Vector2(0f, -26f) * u, Vector2.up, 34f * u, 12f * u);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Sketch(new List<Vector2> { c + dir * 46f * u, c + dir * 62f * u }, 1.2f, 1);
            }
        }

        private void Rui()
        {
            var brim = Arc(P(0f, 118f), 150f * unit, 26f * unit, 0f, 360f, 48);
            Sketch(brim, 3f);
            Sketch(Curve(P(-92f, 124f), P(-86f, 190f), P(-30f, 214f), P(0f, 200f), P(30f, 214f), P(86f, 190f), P(92f, 124f)), 3f);
            Sketch(Curve(P(-88f, 140f), P(0f, 150f), P(88f, 140f)), 4.2f);
            foreach (float s in new[] { -1f, 1f })
                Sketch(Curve(P(s * 44f, -140f), P(s * 64f, -190f), P(s * 30f, -300f)), 2.4f);
            Sketch(new List<Vector2> { P(0f, -130f), P(-12f, -150f), P(0f, -270f), P(12f, -150f), P(0f, -130f) }, 2f);
            Sketch(new List<Vector2> { P(196f, -300f), P(196f, -140f) }, 3f);
            Sketch(Curve(P(196f, -140f), P(196f, -112f), P(222f, -104f), P(236f, -122f)), 3f);
            for (int i = 0; i < 6; i++)
                Sketch(new List<Vector2> { P(-250f + i * 30f, 230f - i * 8f), P(-262f + i * 30f, 200f - i * 8f) }, 1.1f, 1);
        }

        private void Leone()
        {
            Sketch(Curve(P(-90f, 104f), P(-80f, 150f), P(0f, 166f), P(80f, 150f), P(92f, 104f)), 2.8f);
            Sketch(Curve(P(-92f, 104f), P(0f, 92f), P(128f, 96f), P(140f, 82f)), 3f);
            Sketch(Curve(P(-60f, -104f), P(0f, -126f), P(60f, -104f)), 4.6f);
            Sketch(Curve(P(-50f, -122f), P(-60f, -170f), P(-44f, -230f)), 4f);
            Sketch(Curve(P(-28f, -126f), P(-26f, -180f), P(-10f, -214f)), 3.4f);
            for (int i = 0; i < 3; i++)
            {
                float y = -268f + i * 22f;
                float shift = (i - 1) * 6f;
                Sketch(new List<Vector2> { P(-250f + shift, y), P(-120f + shift, y), P(-120f + shift, y + 20f), P(-250f + shift, y + 20f), P(-250f + shift, y) }, 2.2f);
            }
        }

        private void Lucien()
        {
            Sketch(Curve(P(-104f, 96f), P(-60f, 160f), P(30f, 176f), P(110f, 140f), P(96f, 100f)), 3f);
            Sketch(Curve(P(-104f, 96f), P(0f, 82f), P(98f, 100f)), 2.6f);
            Sketch(new List<Vector2> { P(18f, 174f), P(22f, 190f) }, 3f);
            Sketch(Curve(P(-40f, -4f), P(-20f, 8f), P(0f, -2f), P(20f, 8f), P(40f, -4f), P(26f, -22f), P(0f, -14f), P(-26f, -22f), P(-40f, -4f)), 3.2f);
            for (int i = 0; i < 4; i++)
                Sketch(new List<Vector2> { P(-20f + i * 14f, -150f), P(-24f + i * 14f, -200f) }, 1.2f, 1);
            Vector2 jug = P(180f, -250f);
            float u = unit;
            Sketch(Curve(jug + new Vector2(-20f, 50f) * u, jug + new Vector2(-34f, 10f) * u, jug + new Vector2(-30f, -40f) * u, jug + new Vector2(0f, -52f) * u, jug + new Vector2(30f, -40f) * u, jug + new Vector2(34f, 10f) * u, jug + new Vector2(20f, 50f) * u), 2.8f);
            Sketch(new List<Vector2> { jug + new Vector2(-22f, 50f) * u, jug + new Vector2(24f, 50f) * u, jug + new Vector2(30f, 60f) * u }, 2.4f);
            Sketch(Curve(jug + new Vector2(30f, 30f) * u, jug + new Vector2(56f, 22f) * u, jug + new Vector2(54f, -10f) * u, jug + new Vector2(32f, -18f) * u), 2.4f);
        }

        private void Ungaretti()
        {
            Sketch(Curve(P(-80f, 120f), P(-40f, 150f), P(0f, 156f), P(40f, 150f), P(80f, 120f)), 1.4f, 1);
            foreach (float s in new[] { -1f, 1f })
            {
                Sketch(Curve(P(s * 88f, 60f), P(s * 96f, 100f), P(s * 82f, 128f)), 2.2f);
                Sketch(Curve(P(s * 46f, -140f), P(s * 66f, -196f), P(s * 30f, -300f)), 2.4f);
            }
            Sketch(Curve(P(-30f, -116f), P(-8f, -124f), P(8f, -124f), P(30f, -116f)), 2.6f);
            Leaf(P(150f, -220f), new Vector2(0.4f, 0.9f).normalized, 70f * unit, 30f * unit);
            Sketch(Curve(P(150f, -220f), P(140f, -250f), P(126f, -270f)), 2f);
        }

        private Vector2 P(float x, float y) => origin + new Vector2(x, y) * unit;

        private List<Vector2> Curve(params Vector2[] points)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[Mathf.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(points.Length - 1, i + 2)];
                for (int k = 0; k < 10; k++)
                {
                    float t = k / 10f;
                    float t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(points[points.Length - 1]);
            return result;
        }

        private void Sketch(List<Vector2> points, float width, int passes = 2)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var wobbly = new List<Vector2>(points.Count);
                float drift = (float)(random.NextDouble() - 0.5) * 3f * unit;
                for (int i = 0; i < points.Count; i++)
                {
                    float jitter = 1.4f * unit;
                    wobbly.Add(points[i] + new Vector2((float)(random.NextDouble() - 0.5) * jitter + drift, (float)(random.NextDouble() - 0.5) * jitter - drift * 0.5f));
                }
                Stroke(wobbly, Mathf.Max(width * unit, 1.6f) * (pass == 0 ? 1f : 0.55f), 1f);
            }
        }
    }
}
