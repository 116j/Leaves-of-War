using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class PaperProps
{
    private const string Folder = "Assets/_Game/Art/2D/Generated";
    private const int Size = 256;

    private enum Kind { Newspaper, Exercise, Letter, Envelope, Drawing }

    private static readonly (string target, Kind kind, int seed, string caption)[] Targets =
    {
        ("Folded Newspaper 1914", Kind.Newspaper, 1, "This morning's paper. All of Europe talks of nothing but war."),
        ("Exercise Sheets", Kind.Exercise, 2, "My pupils' homework. They conjugate French verbs better than I can put my own thoughts in order."),
        ("Letters and Envelopes", Kind.Letter, 3, "Moammed's letters. He wrote them at night, at this desk. He never sent them."),
        ("Drawing on Floor 1", Kind.Drawing, 11, "One of Antonietto's drawings. He always drew the sun bigger than the house."),
        ("Drawing on Floor 2", Kind.Drawing, 12, "Another drawing. All three of us, hand in hand."),
        ("Drawing on Wall 1", Kind.Drawing, 13, "He hung this one himself, crooked. No one has had the heart to straighten it."),
        ("Drawing on Wall 2", Kind.Drawing, 14, "The garden, with the tree and the butterflies. He said the tree was his."),
        ("Envelope", Kind.Envelope, 4, ""),
    };

    private static readonly HashSet<string> OldCaptions = new HashSet<string>
    {
        "Il giornale di stamattina. In Europa non si parla d'altro che di guerra.",
        "I compiti dei miei ragazzi. Coniugano i verbi francesi meglio di quanto io riesca a mettere in ordine i miei pensieri.",
        "Le lettere di Moammed. Le scriveva di notte, a questa scrivania. Non le ha mai spedite.",
        "Un disegno di Antonietto. Il sole lo faceva sempre più grande della casa.",
        "Un altro disegno. Ci siamo tutti e tre, mano nella mano.",
        "L'aveva appeso lui, storto. Nessuno ha avuto il coraggio di raddrizzarlo.",
        "Il giardino, con l'albero e le farfalle. Diceva che l'albero era suo."
    };

    internal static int Apply(Scene scene, bool point)
    {
        var renderers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToList();
        int count = 0;
        foreach (var (target, kind, seed, caption) in Targets)
        {
            var hits = renderers.Where(r => r.name == target || (r.transform.parent != null && r.transform.parent.name == target && r.name == "Placeholder")).ToList();
            if (hits.Count == 0)
                continue;
            Material material = MaterialFor(kind, seed, point);
            foreach (MeshRenderer r in hits)
            {
                r.sharedMaterial = material;
                Examinable examinable = r.GetComponent<Examinable>();
                bool fresh = examinable == null;
                if (fresh)
                    examinable = r.gameObject.AddComponent<Examinable>();
                var so = new SerializedObject(examinable);
                SerializedProperty text = so.FindProperty("caption");
                if (fresh || string.IsNullOrEmpty(text.stringValue) || OldCaptions.Contains(text.stringValue))
                    text.stringValue = caption;
                if (fresh)
                    so.FindProperty("prompt").stringValue = kind == Kind.Drawing ? "EXAMINE" : "READ";
                SerializedProperty title = so.FindProperty("displayName");
                if (string.IsNullOrEmpty(title.stringValue))
                    title.stringValue = Title(kind, target);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (r.GetComponent<Collider>() == null)
                    r.gameObject.AddComponent<BoxCollider>();
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        return count;
    }

    private static string Title(Kind kind, string target)
    {
        switch (kind)
        {
            case Kind.Newspaper: return "Newspaper, 1914";
            case Kind.Exercise: return "Exercise Sheets";
            case Kind.Letter: return "Moammed's Letters";
            case Kind.Envelope: return "Envelope";
            default: return "Antonietto's Drawing";
        }
    }

    private static Material MaterialFor(Kind kind, int seed, bool point)
    {
        string name = kind + "_" + seed;
        string texturePath = Folder + "/" + name + ".png";
        string materialPath = Folder + "/" + name + ".mat";
        if (!File.Exists(texturePath))
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(texturePath, Draw(kind, seed).EncodeToPNG());
            AssetDatabase.ImportAsset(texturePath);
        }
        if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
        {
            FilterMode filter = point ? FilterMode.Point : FilterMode.Bilinear;
            if (importer.filterMode != filter || importer.mipmapEnabled == point)
            {
                importer.filterMode = filter;
                importer.mipmapEnabled = !point;
                importer.SaveAndReimport();
            }
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.02f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D Draw(Kind kind, int seed)
    {
        var random = new System.Random(seed * 7919);
        var canvas = new Canvas(Size, Size);
        switch (kind)
        {
            case Kind.Newspaper: Newspaper(canvas, random); break;
            case Kind.Exercise: Exercise(canvas, random); break;
            case Kind.Letter: Letter(canvas, random); break;
            case Kind.Envelope: Envelope(canvas, random); break;
            default: Drawing(canvas, random); break;
        }
        return canvas.ToTexture();
    }

    private static void Newspaper(Canvas c, System.Random r)
    {
        c.Fill(new Color(0.86f, 0.82f, 0.7f));
        c.Noise(r, 0.04f);
        Color ink = new Color(0.16f, 0.15f, 0.14f);
        Color grey = new Color(0.42f, 0.4f, 0.37f);
        c.Rect(10, 228, 236, 18, ink);
        c.Rect(10, 222, 236, 2, ink);
        c.Rect(10, 196, 236, 14, new Color(0.25f, 0.24f, 0.22f));
        c.Rect(10, 190, 180, 4, grey);
        int columns = 4;
        int columnWidth = (236 - (columns - 1) * 6) / columns;
        for (int col = 0; col < columns; col++)
        {
            int x = 10 + col * (columnWidth + 6);
            if (col > 0)
                c.Rect(x - 4, 12, 1, 170, grey);
            for (int y = 180; y > 14; y -= 4)
            {
                if (r.NextDouble() < 0.06)
                {
                    c.Rect(x, y - 2, columnWidth, 3, ink);
                    y -= 3;
                    continue;
                }
                int length = y % 37 == 0 ? (int)(columnWidth * (0.3 + r.NextDouble() * 0.5)) : columnWidth;
                c.Rect(x, y, length, 1, grey);
            }
        }
        c.Rect(100, 60, 60, 50, new Color(0.55f, 0.53f, 0.49f));
        c.Vignette(0.25f);
    }

    private static void Exercise(Canvas c, System.Random r)
    {
        c.Fill(new Color(0.95f, 0.93f, 0.86f));
        c.Noise(r, 0.025f);
        Color rule = new Color(0.62f, 0.72f, 0.88f);
        for (int y = 220; y > 10; y -= 12)
            c.Rect(0, y, Size, 1, rule);
        c.Rect(36, 0, 1, Size, new Color(0.85f, 0.45f, 0.45f));
        Color ink = new Color(0.12f, 0.15f, 0.35f);
        for (int y = 222; y > 16; y -= 12)
            if (r.NextDouble() < 0.8)
                c.Handwriting(r, 42, y, 200 - (int)(r.NextDouble() * 60), ink, 3);
        c.Vignette(0.15f);
    }

    private static void Letter(Canvas c, System.Random r)
    {
        c.Fill(new Color(0.9f, 0.84f, 0.68f));
        c.Noise(r, 0.05f);
        Color ink = new Color(0.32f, 0.2f, 0.12f);
        c.Handwriting(r, 150, 232, 80, ink, 3);
        c.Handwriting(r, 24, 212, 90, ink, 3);
        for (int y = 194; y > 50; y -= 11)
        {
            int start = y == 194 ? 40 : 24;
            c.Handwriting(r, start, y, 208 - start - (int)(r.NextDouble() * 30), ink, 3);
        }
        c.Handwriting(r, 140, 30, 80, ink, 4);
        c.Vignette(0.35f);
    }

    private static void Envelope(Canvas c, System.Random r)
    {
        c.Fill(new Color(0.88f, 0.82f, 0.68f));
        c.Noise(r, 0.04f);
        Color fold = new Color(0.72f, 0.66f, 0.52f);
        c.Line(0, 255, 128, 140, fold, 1);
        c.Line(255, 255, 128, 140, fold, 1);
        c.Rect(196, 196, 40, 46, new Color(0.62f, 0.2f, 0.18f));
        c.Rect(200, 200, 32, 38, new Color(0.78f, 0.35f, 0.3f));
        Color ink = new Color(0.25f, 0.17f, 0.12f);
        for (int y = 110; y > 50; y -= 14)
            c.Handwriting(r, 70, y, 140, ink, 3);
        c.Vignette(0.3f);
    }

    private static void Drawing(Canvas c, System.Random r)
    {
        c.Fill(new Color(0.96f, 0.95f, 0.9f));
        c.Noise(r, 0.02f);
        Color[] crayons =
        {
            new Color(0.85f, 0.2f, 0.18f), new Color(0.2f, 0.45f, 0.85f), new Color(0.95f, 0.75f, 0.1f),
            new Color(0.2f, 0.6f, 0.25f), new Color(0.55f, 0.3f, 0.15f), new Color(0.6f, 0.25f, 0.65f)
        };
        Color Pick() => crayons[r.Next(crayons.Length)];
        for (int x = 0; x < Size; x += 3)
            c.Line(x, 20 + r.Next(6), x + r.Next(-2, 3), 34 + r.Next(10), crayons[3], 2);
        int sx = 190 + r.Next(-20, 20), sy = 205 + r.Next(-10, 10);
        c.Circle(sx, sy, 16, crayons[2], 3);
        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.PI * 2f / 9f;
            c.Line(sx + (int)(Mathf.Cos(a) * 22), sy + (int)(Mathf.Sin(a) * 22), sx + (int)(Mathf.Cos(a) * 32), sy + (int)(Mathf.Sin(a) * 32), crayons[2], 2);
        }
        int kind = r.Next(3);
        if (kind == 0)
        {
            Color wall = Pick();
            int hx = 40 + r.Next(30), hy = 40;
            c.Box(hx, hy, 80, 70, wall, 3);
            c.Line(hx - 6, hy + 70, hx + 40, hy + 115, Pick(), 3);
            c.Line(hx + 86, hy + 70, hx + 40, hy + 115, Pick(), 3);
            c.Box(hx + 30, hy, 20, 35, Pick(), 2);
            c.Box(hx + 10, hy + 42, 16, 16, crayons[1], 2);
        }
        else if (kind == 1)
        {
            for (int k = 0; k < 2; k++)
            {
                int fx = 60 + k * 70 + r.Next(-10, 10), fy = 40;
                Color body = Pick();
                c.Circle(fx, fy + 90, 14, body, 2);
                c.Line(fx, fy + 76, fx, fy + 36, body, 3);
                c.Line(fx, fy + 66, fx - 22, fy + 52 + r.Next(20), body, 2);
                c.Line(fx, fy + 66, fx + 22, fy + 52 + r.Next(20), body, 2);
                c.Line(fx, fy + 36, fx - 14, fy + 2, body, 2);
                c.Line(fx, fy + 36, fx + 14, fy + 2, body, 2);
            }
        }
        else
        {
            Color trunk = crayons[4];
            int tx = 80 + r.Next(40);
            c.Box(tx, 34, 16, 70, trunk, 3);
            c.Circle(tx + 8, 130, 40, crayons[3], 4);
            for (int i = 0; i < 6; i++)
                c.Circle(tx - 20 + r.Next(56), 110 + r.Next(40), 5, crayons[0], 3);
            c.Line(170, 60, 230, 60, crayons[1], 3);
            c.Line(170, 60, 200, 110, crayons[1], 3);
            c.Line(230, 60, 200, 110, crayons[1], 3);
        }
        for (int i = 0; i < 4; i++)
        {
            int x = r.Next(20, 230), y = r.Next(150, 240);
            c.Line(x, y, x + r.Next(-30, 30), y + r.Next(-15, 15), Pick(), 2);
        }
    }

    private sealed class Canvas
    {
        private readonly int w, h;
        private readonly Color[] px;

        public Canvas(int w, int h)
        {
            this.w = w;
            this.h = h;
            px = new Color[w * h];
        }

        public void Fill(Color c)
        {
            for (int i = 0; i < px.Length; i++)
                px[i] = c;
        }

        public void Noise(System.Random r, float amount)
        {
            for (int i = 0; i < px.Length; i++)
            {
                float n = ((float)r.NextDouble() - 0.5f) * amount;
                px[i] = new Color(px[i].r + n, px[i].g + n, px[i].b + n, 1f);
            }
        }

        public void Vignette(float amount)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - w * 0.5f) / (w * 0.5f), dy = (y - h * 0.5f) / (h * 0.5f);
                    float d = Mathf.Clamp01((dx * dx + dy * dy) * 0.5f);
                    Color c = px[y * w + x];
                    px[y * w + x] = Color.Lerp(c, c * new Color(0.75f, 0.68f, 0.52f, 1f), d * amount * 2f);
                }
        }

        public void Set(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
                return;
            px[y * w + x] = c;
        }

        public void Rect(int x, int y, int rw, int rh, Color c)
        {
            for (int j = y; j < y + rh; j++)
                for (int i = x; i < x + rw; i++)
                    Set(i, j, c);
        }

        public void Line(int x0, int y0, int x1, int y1, Color c, int width)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int dy = -(width / 2); dy <= width / 2; dy++)
                    for (int dx = -(width / 2); dx <= width / 2; dx++)
                        Set(x + dx, y + dy, c);
            }
        }

        public void Box(int x, int y, int bw, int bh, Color c, int width)
        {
            Line(x, y, x + bw, y, c, width);
            Line(x + bw, y, x + bw, y + bh, c, width);
            Line(x + bw, y + bh, x, y + bh, c, width);
            Line(x, y + bh, x, y, c, width);
        }

        public void Circle(int cx, int cy, int radius, Color c, int width)
        {
            int steps = Mathf.Max(12, radius * 4);
            for (int s = 0; s < steps; s++)
            {
                float a0 = s * Mathf.PI * 2f / steps, a1 = (s + 1) * Mathf.PI * 2f / steps;
                Line(cx + (int)(Mathf.Cos(a0) * radius), cy + (int)(Mathf.Sin(a0) * radius),
                    cx + (int)(Mathf.Cos(a1) * radius), cy + (int)(Mathf.Sin(a1) * radius), c, width);
            }
        }

        public void Handwriting(System.Random r, int x, int y, int length, Color ink, int amplitude)
        {
            int cursor = x;
            int end = x + length;
            while (cursor < end)
            {
                int word = r.Next(8, 26);
                int px0 = cursor, py0 = y;
                for (int i = 1; i <= word && cursor + i < end; i++)
                {
                    int nx = cursor + i;
                    int ny = y + Mathf.RoundToInt(Mathf.Sin((nx + r.Next(0, 3)) * 0.9f) * amplitude * (float)r.NextDouble());
                    Line(px0, py0, nx, ny, ink, 1);
                    px0 = nx;
                    py0 = ny;
                }
                cursor += word + r.Next(4, 8);
            }
        }

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels(px);
            texture.Apply();
            return texture;
        }
    }
}
