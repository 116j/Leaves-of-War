using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PeriodClothes
{
    private const string CharacterFolder = "Assets/_Game/Art/Characters/MakeHuman";
    private const string Folder = CharacterFolder + "/_Outfits";
    private const string ModelChild = "CC0 Character";
    private const string Prefix = "Outfit ";
    private const string Suffix = " Dressed";
    private const string PsxKey = "LeavesOfWar.CC0.Psx";
    private const float Circumference = 0.95f;
    private const int Ring = 24;

    private enum Hat { None, Keep, Kepi, Bowler, Beret }
    private enum Kind { Body, Garment, Shoes, Hat, Other }
    private enum Grp { Torso, Head, UpperArm, LowerArm, Hand, Thigh, Calf }
    private enum Region { Keep = -1, Torso = 0, SleeveL = 1, SleeveR = 2, Legs = 3 }

    private sealed class Outfit
    {
        public string role;
        public string look;
        public Color jacket, trousers, shirt, buttons, shoes;
        public Color tie, waistcoat, belt, puttees, mud, hatColor, hatTrim;
        public bool lapels, pockets, armband, stars, coat, longSleeves;
        public float roll;
        public Hat hat = Hat.None;
    }

    private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    private static readonly Outfit[] Outfits =
    {
        new Outfit
        {
            role = "Carlo", look = "shirt with rolled-up sleeves, waistcoat, wool trousers",
            shirt = C(0.80f, 0.77f, 0.68f), waistcoat = C(0.25f, 0.19f, 0.14f), trousers = C(0.29f, 0.27f, 0.25f),
            buttons = C(0.12f, 0.10f, 0.08f), shoes = C(0.20f, 0.13f, 0.09f), roll = 0.62f,
        },
        new Outfit
        {
            role = "Apollinaire", look = "dark three-piece suit, white collar, tie, bowler hat",
            jacket = C(0.21f, 0.20f, 0.19f), trousers = C(0.21f, 0.20f, 0.19f), shirt = C(0.86f, 0.85f, 0.80f), tie = C(0.10f, 0.09f, 0.10f),
            buttons = C(0.10f, 0.09f, 0.08f), shoes = C(0.07f, 0.07f, 0.07f), lapels = true, pockets = true,
            hat = Hat.Bowler, hatColor = C(0.07f, 0.07f, 0.07f), hatTrim = C(0.03f, 0.03f, 0.03f),
        },
        new Outfit
        {
            role = "Moammed", look = "long dark overcoat, white collar, dark tie",
            jacket = C(0.13f, 0.13f, 0.15f), trousers = C(0.22f, 0.22f, 0.23f), shirt = C(0.84f, 0.83f, 0.78f), tie = C(0.09f, 0.09f, 0.10f),
            buttons = C(0.06f, 0.06f, 0.06f), shoes = C(0.07f, 0.07f, 0.07f), lapels = true, coat = true,
        },
        new Outfit
        {
            role = "Pietro", look = "grigioverde M1909 uniform, collar stars, belt and pouches, puttees, Red Cross armband, kepi, mud to the knees",
            jacket = C(0.42f, 0.44f, 0.36f), trousers = C(0.42f, 0.44f, 0.36f), shirt = C(0.42f, 0.44f, 0.36f), puttees = C(0.38f, 0.40f, 0.32f),
            buttons = C(0.30f, 0.31f, 0.26f), belt = C(0.30f, 0.20f, 0.12f), mud = C(0.30f, 0.24f, 0.17f), shoes = C(0.19f, 0.14f, 0.10f),
            armband = true, stars = true, longSleeves = true,
            hat = Hat.Kepi, hatColor = C(0.42f, 0.44f, 0.36f), hatTrim = C(0.08f, 0.07f, 0.06f),
        },
        new Outfit
        {
            role = "Companion", look = "grigioverde uniform, puttees, kepi",
            jacket = C(0.40f, 0.42f, 0.34f), trousers = C(0.40f, 0.42f, 0.34f), shirt = C(0.40f, 0.42f, 0.34f), puttees = C(0.36f, 0.38f, 0.30f),
            buttons = C(0.30f, 0.31f, 0.26f), belt = C(0.28f, 0.19f, 0.11f), mud = new Color(0.28f, 0.23f, 0.16f, 0.6f), shoes = C(0.17f, 0.13f, 0.09f),
            stars = true, longSleeves = true,
            hat = Hat.Kepi, hatColor = C(0.40f, 0.42f, 0.34f), hatTrim = C(0.08f, 0.07f, 0.06f),
        },
        new Outfit
        {
            role = "Rui", look = "light linen suit, white shirt, burgundy tie, his fedora",
            jacket = C(0.62f, 0.58f, 0.50f), trousers = C(0.60f, 0.56f, 0.48f), shirt = C(0.88f, 0.87f, 0.83f), tie = C(0.38f, 0.11f, 0.12f),
            buttons = C(0.30f, 0.24f, 0.18f), shoes = C(0.26f, 0.15f, 0.09f), lapels = true, pockets = true, hat = Hat.Keep,
        },
        new Outfit
        {
            role = "Leone", look = "brown tweed jacket, grey trousers, white shirt, green tie",
            jacket = C(0.36f, 0.28f, 0.20f), trousers = C(0.32f, 0.32f, 0.33f), shirt = C(0.85f, 0.84f, 0.80f), tie = C(0.16f, 0.22f, 0.16f),
            buttons = C(0.15f, 0.11f, 0.08f), shoes = C(0.18f, 0.11f, 0.07f), lapels = true, pockets = true,
        },
        new Outfit
        {
            role = "Lucien", look = "linen shirt with rolled sleeves, dark waistcoat, brown trousers, beret",
            shirt = C(0.72f, 0.70f, 0.62f), waistcoat = C(0.20f, 0.20f, 0.22f), trousers = C(0.33f, 0.25f, 0.17f),
            buttons = C(0.10f, 0.10f, 0.10f), shoes = C(0.15f, 0.11f, 0.08f), mud = new Color(0.30f, 0.25f, 0.18f, 0.35f),
            hat = Hat.Beret, hatColor = C(0.10f, 0.10f, 0.13f), hatTrim = C(0.06f, 0.06f, 0.08f),
        },
    };

    private static readonly string[] Garments = { "suit", "shirt", "pants", "trouser", "jacket", "dress", "coat", "jeans", "sweater", "tshirt", "vest", "skirt", "overall" };
    private static readonly string[] Fingers = { "index", "middle", "ring", "pinky", "thumb", "finger" };

    private sealed class Part
    {
        public SkinnedMeshRenderer renderer;
        public Mesh source;
        public Kind kind;
        public string[] bones;
        public Matrix4x4[] bind;
        public Vector3[] local;
        public Vector3[] vertices;
        public BoneWeight[] weights;
        public Matrix4x4 toRef;
        public Grp[] group;
        public int[] side;
        public Region[] regions;
    }

    private sealed class BoneInfo
    {
        public string name;
        public string lower;
        public Vector3 pos;
        public Grp g;
        public int side;
    }

    private sealed class Frame
    {
        public Vector3 up, right, fwd, pelvis, waist, chest, neck, head;
        public readonly Vector3[] shoulder = new Vector3[2], elbow = new Vector3[2], wrist = new Vector3[2];
        public readonly Vector3[] hip = new Vector3[2], knee = new Vector3[2], ankle = new Vector3[2];
        public string pelvisName, chestName, neckName, headName;
        public readonly string[] hipName = new string[2];
        public float unit, floor, top, yWaist, yChest, yNeck, yHip, yKnee, yCrotch, torsoLo, torsoHi, cx, cz, garmentTop, upperArm;
        public float Y(Vector3 p) => Vector3.Dot(p, up);
        public float X(Vector3 p) => Vector3.Dot(p, right);
        public float Z(Vector3 p) => Vector3.Dot(p, fwd);
        public Vector3 At(float x, float y, float z) => right * x + up * y + fwd * z;
        public Vector3 Dir(float a) => right * Mathf.Sin(a) + fwd * Mathf.Cos(a);
    }

    [MenuItem("Tools/Leaves of War/Characters/Dress characters in open scene")]
    private static void Menu()
    {
        Scene scene = SceneManager.GetActiveScene();
        var log = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(x => x.name == ModelChild).ToList())
            {
                string result = Dress(t.gameObject, RoleOf(t));
                if (!string.IsNullOrEmpty(result))
                    log.Add(result);
            }
        if (log.Count > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        string text = log.Count > 0
            ? string.Join("\n", log) + "\n\nSave the scene (Ctrl+S)."
            : "No MakeHuman character in the open scene: run Characters > Apply characters to open scene first.";
        Debug.Log("Period clothes\n" + text);
        EditorUtility.DisplayDialog("Period clothes", text, "OK");
    }

    private static string RoleOf(Transform model)
    {
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject) ?? "";
        foreach (Outfit o in Outfits)
            if (path.IndexOf("/" + o.role + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                return o.role;
        for (Transform p = model.parent; p != null; p = p.parent)
        {
            DialogueSpeaker speaker = p.GetComponent<DialogueSpeaker>();
            foreach (Outfit o in Outfits)
                if (string.Equals(p.name, o.role, StringComparison.OrdinalIgnoreCase) ||
                    speaker != null && string.Equals(speaker.SpeakerId, o.role, StringComparison.OrdinalIgnoreCase))
                    return o.role;
        }
        return null;
    }

    internal static string Dress(GameObject instance, string role)
    {
        Outfit o = Outfits.FirstOrDefault(x => string.Equals(x.role, role, StringComparison.OrdinalIgnoreCase));
        if (o == null || instance == null)
            return null;

        foreach (Transform old in instance.GetComponentsInChildren<Transform>(true).Where(x => x != instance.transform && x.name.StartsWith(Prefix)).ToList())
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

        var parts = new List<Part>();
        foreach (SkinnedMeshRenderer r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh source = SourceMesh(r, o.role);
            if (source != null)
                parts.Add(Read(r, source, KindOf(r, source)));
        }
        Part body = parts.Where(p => p.kind == Kind.Body).OrderByDescending(p => p.source.vertexCount).FirstOrDefault()
                    ?? parts.OrderByDescending(p => p.source.vertexCount).FirstOrDefault();
        if (body == null)
            return o.role + ": no skinned meshes, nothing dressed.";
        parts.RemoveAll(p => !Link(p, body));

        Frame f = Measure(parts);
        if (f == null)
            return o.role + ": skeleton not recognised (export with the Game engine skeleton), nothing dressed.";
        Heights(f, parts);

        foreach (Part p in parts)
            for (int i = 0; i < p.side.Length; i++)
                if (p.side[i] == 0)
                    p.side[i] = f.X(p.vertices[i]) - f.cx >= 0f ? 1 : -1;

        List<Part> garments = parts.Where(p => p.kind == Kind.Garment).ToList();
        if (garments.Count == 0)
            return o.role + ": no clothes mesh found in the MakeHuman export (add any suit in MakeHuman), nothing dressed.";
        foreach (Part p in parts)
            p.regions = Regions(p, f, o);

        f.garmentTop = float.MinValue;
        foreach (Part p in garments)
            for (int i = 0; i < p.vertices.Length; i++)
                if (p.regions[i] == Region.Torso)
                    f.garmentTop = Mathf.Max(f.garmentTop, f.Y(p.vertices[i]));
        if (f.garmentTop == float.MinValue)
            f.garmentTop = f.yNeck;
        f.torsoLo = f.yKnee;
        f.torsoHi = Mathf.Max(f.garmentTop, f.yNeck) + 0.05f * f.unit;

        var sleeveEnd = new[] { -1f, -1f };
        foreach (Part p in garments)
            for (int i = 0; i < p.vertices.Length; i++)
                if (p.regions[i] == Region.SleeveL || p.regions[i] == Region.SleeveR)
                {
                    int s = p.regions[i] == Region.SleeveL ? 0 : 1;
                    sleeveEnd[s] = Mathf.Max(sleeveEnd[s], Uv(f, p.regions[i], p, i).y * 1.5f - 0.2f);
                }

        string folder = Folder + "/" + o.role;
        EnsureFolder(folder);
        Color skin = SkinColor(body);
        string name = o.role + " ";
        Material torso = Mat(folder + "/" + name + "Torso.mat", Color.white,
            Save(Paint(512, 512, (u, v, i, j) => Torso(o, f, u, v, i, j)), folder + "/" + name + "Torso.png", true), 0.08f, false);
        Material sleeveL = Mat(folder + "/" + name + "Sleeve L.mat", Color.white,
            Save(Paint(256, 256, (u, v, i, j) => Sleeve(o, f, 0, sleeveEnd[0], skin, u, v, i, j)), folder + "/" + name + "Sleeve L.png", true), 0.08f, false);
        Material sleeveR = Mat(folder + "/" + name + "Sleeve R.mat", Color.white,
            Save(Paint(256, 256, (u, v, i, j) => Sleeve(o, f, 1, sleeveEnd[1], skin, u, v, i, j)), folder + "/" + name + "Sleeve R.png", true), 0.08f, false);
        Material legs = Mat(folder + "/" + name + "Legs.mat", Color.white,
            Save(Paint(256, 256, (u, v, i, j) => Leg(o, u, v, i, j)), folder + "/" + name + "Legs.png", true), 0.08f, false);
        var regionMaterials = new[] { torso, sleeveL, sleeveR, legs };

        Color shoeColor = o.mud.a > 0.5f ? Color.Lerp(o.shoes, o.mud, 0.5f) : o.shoes;
        Material shoes = Mat(folder + "/" + name + "Shoes.mat", shoeColor, null, 0.3f, false);

        var done = new List<string>();
        foreach (Part p in parts)
        {
            switch (p.kind)
            {
                case Kind.Garment:
                    Apply(p, f, regionMaterials, folder, o.role);
                    break;
                case Kind.Body:
                    if (o.longSleeves && p.regions.Any(r => r != Region.Keep))
                        Apply(p, f, regionMaterials, folder, o.role);
                    else
                        Restore(p);
                    break;
                case Kind.Shoes:
                    p.renderer.sharedMaterials = Enumerable.Repeat(shoes, Mathf.Max(1, p.source.subMeshCount)).ToArray();
                    break;
                case Kind.Hat:
                    p.renderer.enabled = o.hat == Hat.Keep;
                    break;
            }
        }

        int layer = instance.layer;
        if (o.stars || o.longSleeves && f.garmentTop < f.yNeck)
        {
            Material collar = Mat(folder + "/" + name + "Collar.mat", Color.white,
                Save(Paint(256, 64, (u, v, i, j) => Collar(o, u, v, i, j)), folder + "/" + name + "Collar.png", true), 0.08f, true);
            if (BuildCollar(f, parts, body, collar, folder + "/" + name + "Collar.asset", layer))
                done.Add("collar");
        }
        Part main = garments.OrderByDescending(p => p.source.vertexCount).First();
        if (o.coat)
        {
            Material coat = Mat(folder + "/" + name + "Coat.mat", Color.white,
                Save(Paint(256, 128, (u, v, i, j) => Skirt(o, u, v, i, j)), folder + "/" + name + "Coat.png", true), 0.08f, true);
            if (BuildCoat(f, parts, main, coat, folder + "/" + name + "Coat.asset", layer))
                done.Add("coat tails");
        }
        if (o.hat != Hat.None && o.hat != Hat.Keep)
        {
            Material felt = Mat(folder + "/" + name + "Hat.mat", o.hatColor, null, 0.05f, true);
            Material trim = Mat(folder + "/" + name + "Hat Trim.mat", o.hatTrim, null, 0.35f, true);
            if (BuildHat(o, f, parts, body, felt, trim, folder + "/" + name + "Hat.asset", layer))
                done.Add(o.hat.ToString().ToLowerInvariant());
        }
        AssetDatabase.SaveAssets();
        return o.role + ": " + o.look + (done.Count > 0 ? " (" + string.Join(", ", done) + " built)" : "") + ".";
    }

    private static Kind KindOf(SkinnedMeshRenderer r, Mesh source)
    {
        string n = (r.name + " " + source.name + " " + string.Join(" ", r.sharedMaterials.Where(m => m != null).Select(m => m.name))).ToLowerInvariant();
        if (n.Contains("proxy") || n.Contains("body") || n.Contains("skin") || n.Contains("basemesh"))
            return Kind.Body;
        if (n.Contains("shoe") || n.Contains("boot"))
            return Kind.Shoes;
        if (n.Contains("fedora") || n.Contains("hat") || n.Contains("helmet") || n.Contains("beret"))
            return Kind.Hat;
        if (Garments.Any(n.Contains))
            return Kind.Garment;
        return Kind.Other;
    }

    private static Mesh SourceMesh(SkinnedMeshRenderer r, string role)
    {
        SkinnedMeshRenderer original = PrefabUtility.GetCorrespondingObjectFromSource(r);
        if (original != null && original.sharedMesh != null)
            return original.sharedMesh;
        Mesh mesh = r.sharedMesh;
        if (mesh == null)
            return null;
        if (!AssetDatabase.GetAssetPath(mesh).StartsWith(Folder))
            return mesh;
        string wanted = mesh.name.EndsWith(Suffix) ? mesh.name.Substring(0, mesh.name.Length - Suffix.Length) : mesh.name;
        if (AssetDatabase.IsValidFolder(CharacterFolder + "/" + role))
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterFolder + "/" + role }))
                foreach (Mesh candidate in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Mesh>())
                    if (candidate.name == wanted)
                        return candidate;
        return mesh;
    }

    private static Part Read(SkinnedMeshRenderer r, Mesh source, Kind kind)
    {
        var p = new Part { renderer = r, source = source, kind = kind };
        Transform[] bones = r.bones;
        p.bind = source.bindposes;
        p.bones = new string[p.bind.Length];
        for (int i = 0; i < p.bones.Length; i++)
            p.bones[i] = i < bones.Length && bones[i] != null ? bones[i].name : "";
        p.local = source.vertices;
        p.weights = source.boneWeights;
        return p;
    }

    private static bool Link(Part p, Part reference)
    {
        if (p == reference)
        {
            p.toRef = Matrix4x4.identity;
        }
        else
        {
            int j = -1, k = -1;
            for (int a = 0; a < p.bones.Length && j < 0; a++)
            {
                if (p.bones[a] == "")
                    continue;
                int c = Array.IndexOf(reference.bones, p.bones[a]);
                if (c >= 0)
                {
                    j = a;
                    k = c;
                }
            }
            if (j < 0)
                return false;
            p.toRef = reference.bind[k].inverse * p.bind[j];
        }
        int n = p.local.Length;
        p.vertices = new Vector3[n];
        for (int i = 0; i < n; i++)
            p.vertices[i] = p.toRef.MultiplyPoint3x4(p.local[i]);

        var boneGroup = new Grp[p.bones.Length];
        var boneSide = new int[p.bones.Length];
        for (int b = 0; b < p.bones.Length; b++)
            boneGroup[b] = GroupOf(p.bones[b], out boneSide[b]);
        p.group = new Grp[n];
        p.side = new int[n];
        var acc = new float[21];
        bool skinned = p.weights != null && p.weights.Length == n;
        for (int i = 0; i < n; i++)
        {
            if (!skinned)
                continue;
            Array.Clear(acc, 0, acc.Length);
            BoneWeight w = p.weights[i];
            Accumulate(acc, boneGroup, boneSide, w.boneIndex0, w.weight0);
            Accumulate(acc, boneGroup, boneSide, w.boneIndex1, w.weight1);
            Accumulate(acc, boneGroup, boneSide, w.boneIndex2, w.weight2);
            Accumulate(acc, boneGroup, boneSide, w.boneIndex3, w.weight3);
            int best = 0;
            for (int k = 1; k < acc.Length; k++)
                if (acc[k] > acc[best])
                    best = k;
            p.group[i] = (Grp)(best / 3);
            p.side[i] = best % 3 - 1;
        }
        return true;
    }

    private static void Accumulate(float[] acc, Grp[] groups, int[] sides, int bone, float weight)
    {
        if (weight <= 0f || bone < 0 || bone >= groups.Length)
            return;
        acc[(int)groups[bone] * 3 + sides[bone] + 1] += weight;
    }

    private static Grp GroupOf(string bone, out int side)
    {
        string n = (bone ?? "").ToLowerInvariant();
        side = n.EndsWith("_l") || n.EndsWith(".l") || n.Contains("left") ? -1 : n.EndsWith("_r") || n.EndsWith(".r") || n.Contains("right") ? 1 : 0;
        if (Fingers.Any(n.Contains) || n.Contains("hand") || n.Contains("wrist"))
            return Grp.Hand;
        if (n.Contains("lowerarm") || n.Contains("forearm"))
            return Grp.LowerArm;
        if (n.Contains("arm"))
            return Grp.UpperArm;
        if (n.Contains("thigh") || n.Contains("upleg") || n.Contains("upperleg"))
            return Grp.Thigh;
        if (n.Contains("calf") || n.Contains("shin") || n.Contains("leg") || n.Contains("knee") || n.Contains("foot") || n.Contains("ankle") || n.Contains("ball") || n.Contains("toe"))
            return Grp.Calf;
        if (n.Contains("head") || n.Contains("jaw") || n.Contains("eye"))
            return Grp.Head;
        side = 0;
        return Grp.Torso;
    }

    private static Frame Measure(List<Part> parts)
    {
        var bones = new List<BoneInfo>();
        var seen = new HashSet<string>();
        foreach (Part p in parts)
            for (int j = 0; j < p.bones.Length; j++)
            {
                string n = p.bones[j];
                if (string.IsNullOrEmpty(n) || !seen.Add(n))
                    continue;
                var info = new BoneInfo { name = n, lower = n.ToLowerInvariant(), pos = p.toRef.MultiplyPoint3x4(p.bind[j].inverse.MultiplyPoint3x4(Vector3.zero)) };
                info.g = GroupOf(n, out info.side);
                bones.Add(info);
            }

        BoneInfo Pick(Func<BoneInfo, bool> match, bool highest = false)
        {
            List<BoneInfo> list = bones.Where(match).OrderBy(b => b.pos.y).ToList();
            return list.Count == 0 ? null : highest ? list[list.Count - 1] : list[0];
        }

        bool Plain(BoneInfo b) => !b.lower.Contains("twist") && !b.lower.Contains("end") && !b.lower.Contains("nub") && !b.lower.Contains("top");

        BoneInfo spineLow = Pick(b => b.lower.Contains("spine") || b.lower.Contains("chest"));
        BoneInfo spineHigh = Pick(b => b.lower.Contains("spine") || b.lower.Contains("chest"), true);
        BoneInfo pelvis = Pick(b => b.lower.Contains("pelvis") || b.lower.Contains("hips")) ?? spineLow;
        BoneInfo neck = Pick(b => b.lower.Contains("neck") && Plain(b));
        BoneInfo head = Pick(b => b.lower.Contains("head") && Plain(b));
        if (spineLow == null || pelvis == null || neck == null || head == null)
            return null;

        var f = new Frame
        {
            pelvis = pelvis.pos, waist = spineLow.pos, chest = spineHigh.pos, neck = neck.pos, head = head.pos,
            pelvisName = pelvis.name, chestName = spineHigh.name, neckName = neck.name, headName = head.name,
        };
        for (int s = 0; s < 2; s++)
        {
            int side = s == 0 ? -1 : 1;
            BoneInfo sh = Pick(b => b.g == Grp.UpperArm && b.side == side && Plain(b));
            BoneInfo el = Pick(b => b.g == Grp.LowerArm && b.side == side && Plain(b), true);
            BoneInfo wr = Pick(b => b.g == Grp.Hand && b.side == side && (b.lower.Contains("hand") || b.lower.Contains("wrist")) && Plain(b), true);
            BoneInfo hp = Pick(b => b.g == Grp.Thigh && b.side == side && Plain(b), true);
            BoneInfo kn = Pick(b => b.g == Grp.Calf && b.side == side && Plain(b) && !b.lower.Contains("foot") && !b.lower.Contains("ankle") && !b.lower.Contains("ball") && !b.lower.Contains("toe"), true);
            BoneInfo an = Pick(b => b.side == side && (b.lower.Contains("foot") || b.lower.Contains("ankle")) && Plain(b), true);
            if (sh == null || el == null || wr == null || hp == null || kn == null || an == null)
                return null;
            f.shoulder[s] = sh.pos;
            f.elbow[s] = el.pos;
            f.wrist[s] = wr.pos;
            f.hip[s] = hp.pos;
            f.knee[s] = kn.pos;
            f.ankle[s] = an.pos;
            f.hipName[s] = hp.name;
        }

        f.up = (f.head - (f.ankle[0] + f.ankle[1]) * 0.5f).normalized;
        Vector3 right = f.shoulder[1] - f.shoulder[0];
        right -= f.up * Vector3.Dot(right, f.up);
        if (right.sqrMagnitude < 1e-10f || f.up.sqrMagnitude < 0.5f)
            return null;
        f.right = right.normalized;
        f.fwd = Vector3.Cross(f.right, f.up);
        return f;
    }

    private static void Heights(Frame f, List<Part> parts)
    {
        f.floor = float.MaxValue;
        f.top = float.MinValue;
        foreach (Part p in parts)
            for (int i = 0; i < p.vertices.Length; i++)
            {
                float y = f.Y(p.vertices[i]);
                if (p.kind == Kind.Body || p.kind == Kind.Garment || p.kind == Kind.Shoes)
                    f.floor = Mathf.Min(f.floor, y);
                if ((p.kind == Kind.Body || p.kind == Kind.Other) && p.group[i] == Grp.Head)
                    f.top = Mathf.Max(f.top, y);
            }
        if (f.top == float.MinValue)
            f.top = f.Y(f.head) + (f.Y(f.head) - f.Y(f.neck)) * 2f;
        if (f.floor == float.MaxValue)
            f.floor = Mathf.Min(f.Y(f.ankle[0]), f.Y(f.ankle[1]));
        f.unit = Mathf.Max(1e-4f, (f.top - f.floor) / 1.75f);
        f.yWaist = f.Y(f.waist);
        f.yChest = f.Y(f.chest);
        f.yNeck = f.Y(f.neck);
        f.yHip = (f.Y(f.hip[0]) + f.Y(f.hip[1])) * 0.5f;
        f.yKnee = (f.Y(f.knee[0]) + f.Y(f.knee[1])) * 0.5f;
        f.yCrotch = f.yHip - 0.18f * (f.yHip - f.yKnee);
        f.cx = (f.X(f.pelvis) + f.X(f.chest)) * 0.5f;
        f.cz = (f.Z(f.pelvis) + f.Z(f.chest)) * 0.5f;
        f.upperArm = (f.elbow[0] - f.shoulder[0]).magnitude / f.unit;
    }

    private static Region[] Regions(Part p, Frame f, Outfit o)
    {
        int n = p.vertices.Length;
        var r = new Region[n];
        for (int i = 0; i < n; i++)
            r[i] = Region.Keep;
        if (p.kind == Kind.Body)
        {
            if (o.longSleeves)
                for (int i = 0; i < n; i++)
                {
                    Region sleeve = p.side[i] < 0 ? Region.SleeveL : Region.SleeveR;
                    if (p.group[i] == Grp.UpperArm || p.group[i] == Grp.LowerArm || p.group[i] == Grp.Hand && Uv(f, sleeve, p, i).y * 1.5f - 0.2f < 1.04f)
                        r[i] = sleeve;
                }
            return r;
        }
        if (p.kind != Kind.Garment)
            return r;

        for (int i = 0; i < n; i++)
        {
            switch (p.group[i])
            {
                case Grp.UpperArm:
                case Grp.LowerArm:
                case Grp.Hand:
                    r[i] = p.side[i] < 0 ? Region.SleeveL : Region.SleeveR;
                    break;
                case Grp.Thigh:
                case Grp.Calf:
                    r[i] = Region.Legs;
                    break;
                default:
                    r[i] = Region.Torso;
                    break;
            }
        }

        int[] comp = Components(p, f.unit * 0.0008f);
        var minY = new Dictionary<int, float>();
        var legCount = new Dictionary<int, int>();
        var otherCount = new Dictionary<int, int>();
        for (int i = 0; i < n; i++)
        {
            int c = comp[i];
            float y = f.Y(p.vertices[i]);
            minY[c] = minY.TryGetValue(c, out float m) ? Mathf.Min(m, y) : y;
            if (r[i] == Region.Legs)
                legCount[c] = (legCount.TryGetValue(c, out int a) ? a : 0) + 1;
            else
                otherCount[c] = (otherCount.TryGetValue(c, out int b) ? b : 0) + 1;
        }
        for (int i = 0; i < n; i++)
        {
            int c = comp[i];
            if (r[i] != Region.Legs || minY[c] <= f.yKnee)
                continue;
            int legs = legCount.TryGetValue(c, out int a) ? a : 0;
            int other = otherCount.TryGetValue(c, out int b) ? b : 0;
            if (legs < other)
                r[i] = Region.Torso;
        }
        return r;
    }

    private static int[] Components(Part p, float quantum)
    {
        int n = p.vertices.Length;
        var parent = new int[n];
        for (int i = 0; i < n; i++)
            parent[i] = i;

        int Root(int a)
        {
            while (parent[a] != a)
            {
                parent[a] = parent[parent[a]];
                a = parent[a];
            }
            return a;
        }

        void Join(int a, int b)
        {
            a = Root(a);
            b = Root(b);
            if (a != b)
                parent[a] = b;
        }

        var seen = new Dictionary<Vector3Int, int>();
        float q = Mathf.Max(quantum, 1e-6f);
        for (int i = 0; i < n; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(p.vertices[i] / q);
            if (seen.TryGetValue(key, out int other))
                Join(i, other);
            else
                seen[key] = i;
        }
        for (int s = 0; s < p.source.subMeshCount; s++)
        {
            if (p.source.GetTopology(s) != MeshTopology.Triangles)
                continue;
            int[] tris = p.source.GetTriangles(s);
            for (int k = 0; k + 2 < tris.Length; k += 3)
            {
                Join(tris[k], tris[k + 1]);
                Join(tris[k + 1], tris[k + 2]);
            }
        }
        var result = new int[n];
        for (int i = 0; i < n; i++)
            result[i] = Root(i);
        return result;
    }

    private static Vector2 Uv(Frame f, Region region, Part p, int i)
    {
        Vector3 v = p.vertices[i];
        if (region == Region.Torso)
        {
            float u = Mathf.Atan2(f.X(v) - f.cx, f.Z(v) - f.cz) / (2f * Mathf.PI) + 0.5f;
            return new Vector2(u, (f.Y(v) - f.torsoLo) / Mathf.Max(f.torsoHi - f.torsoLo, 1e-4f));
        }
        Grp g = p.group[i];
        bool arm = region != Region.Legs;
        int s = region == Region.SleeveL ? 0 : region == Region.SleeveR ? 1 : p.side[i] < 0 ? 0 : 1;
        bool lower = arm ? g == Grp.LowerArm || g == Grp.Hand : g == Grp.Calf;
        Vector3 a0 = arm ? lower ? f.elbow[s] : f.shoulder[s] : lower ? f.knee[s] : f.hip[s];
        Vector3 a1 = arm ? lower ? f.wrist[s] : f.elbow[s] : lower ? f.ankle[s] : f.knee[s];
        Vector3 axis = a1 - a0;
        float k = Vector3.Dot(v - a0, axis) / Mathf.Max(axis.sqrMagnitude, 1e-8f);
        float t = (lower ? 0.5f : 0f) + k * 0.5f;
        Vector3 dir = axis.normalized;
        Vector3 refv = arm ? f.right * (s == 0 ? -1f : 1f) : f.fwd;
        refv = (refv - dir * Vector3.Dot(refv, dir)).normalized;
        Vector3 other = Vector3.Cross(dir, refv);
        Vector3 d = v - (a0 + axis * k);
        float angle = Mathf.Atan2(Vector3.Dot(d, other), Vector3.Dot(d, refv)) / (2f * Mathf.PI) + 0.5f;
        return new Vector2(angle, (t + 0.2f) / 1.5f);
    }

    private static Region Majority(Region a, Region b, Region c)
    {
        if (a == b || a == c)
            return a;
        if (b == c)
            return b;
        return a;
    }

    private static void Apply(Part p, Frame f, Material[] regionMaterials, string folder, string role)
    {
        Mesh mesh = Rebuild(p, f, p.source.name + Suffix, out List<(Region region, int sub)> layout);
        mesh = SaveMesh(mesh, folder + "/" + role + " " + Clean(p.source.name) + ".asset");
        Material[] current = p.renderer.sharedMaterials;
        var mats = new Material[layout.Count];
        for (int s = 0; s < layout.Count; s++)
        {
            (Region region, int sub) = layout[s];
            mats[s] = region == Region.Keep
                ? current.Length > 0 ? current[Mathf.Clamp(sub, 0, current.Length - 1)] : null
                : regionMaterials[(int)region];
        }
        p.renderer.sharedMesh = mesh;
        p.renderer.sharedMaterials = mats;
        p.renderer.updateWhenOffscreen = true;
    }

    private static void Restore(Part p)
    {
        if (p.renderer.sharedMesh == p.source)
            return;
        Material[] current = p.renderer.sharedMaterials;
        p.renderer.sharedMesh = p.source;
        p.renderer.sharedMaterials = current.Take(Mathf.Max(1, p.source.subMeshCount)).ToArray();
    }

    private static Mesh Rebuild(Part p, Frame f, string name, out List<(Region region, int sub)> layout)
    {
        Mesh src = p.source;
        Vector3[] pos = src.vertices;
        Vector3[] nor = src.normals;
        Vector4[] tan = src.tangents;
        Vector2[] uv = src.uv;
        BoneWeight[] bw = src.boneWeights;
        int count = pos.Length;
        var map = new Dictionary<long, int>();
        var origin = new List<int>();
        var uvs = new List<Vector2>();
        int subs = src.subMeshCount;
        var keep = new List<int>[subs];
        var regionTris = new List<int>[4];
        for (int s = 0; s < subs; s++)
            keep[s] = new List<int>();
        for (int r = 0; r < 4; r++)
            regionTris[r] = new List<int>();

        int Add(int i, int slot, int variant, Vector2 coord)
        {
            long key = ((long)i * 8 + slot + 1) * 2 + variant;
            if (map.TryGetValue(key, out int index))
                return index;
            index = origin.Count;
            map[key] = index;
            origin.Add(i);
            uvs.Add(coord);
            return index;
        }

        var tri = new int[3];
        var coords = new Vector2[3];
        for (int s = 0; s < subs; s++)
        {
            if (src.GetTopology(s) != MeshTopology.Triangles)
                continue;
            int[] tris = src.GetTriangles(s);
            for (int k = 0; k + 2 < tris.Length; k += 3)
            {
                tri[0] = tris[k];
                tri[1] = tris[k + 1];
                tri[2] = tris[k + 2];
                Region r = Majority(p.regions[tri[0]], p.regions[tri[1]], p.regions[tri[2]]);
                if (r == Region.Keep)
                {
                    for (int c = 0; c < 3; c++)
                        keep[s].Add(Add(tri[c], -1, 0, tri[c] < uv.Length ? uv[tri[c]] : Vector2.zero));
                    continue;
                }
                float min = float.MaxValue, max = float.MinValue;
                for (int c = 0; c < 3; c++)
                {
                    coords[c] = Uv(f, r, p, tri[c]);
                    min = Mathf.Min(min, coords[c].x);
                    max = Mathf.Max(max, coords[c].x);
                }
                bool wrap = max - min > 0.5f;
                for (int c = 0; c < 3; c++)
                {
                    int variant = wrap && coords[c].x < 0.5f ? 1 : 0;
                    Vector2 coord = variant == 1 ? coords[c] + Vector2.right : coords[c];
                    regionTris[(int)r].Add(Add(tri[c], (int)r, variant, coord));
                }
            }
        }

        var mesh = new Mesh { name = name };
        if (origin.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(origin.Select(i => pos[i]).ToList());
        if (nor.Length == count)
            mesh.SetNormals(origin.Select(i => nor[i]).ToList());
        if (tan.Length == count)
            mesh.SetTangents(origin.Select(i => tan[i]).ToList());
        mesh.SetUVs(0, uvs);
        if (bw.Length == count)
            mesh.boneWeights = origin.Select(i => bw[i]).ToArray();
        mesh.bindposes = src.bindposes;

        layout = new List<(Region region, int sub)>();
        var lists = new List<List<int>>();
        for (int s = 0; s < subs; s++)
            if (keep[s].Count > 0)
            {
                lists.Add(keep[s]);
                layout.Add((Region.Keep, s));
            }
        for (int r = 0; r < 4; r++)
            if (regionTris[r].Count > 0)
            {
                lists.Add(regionTris[r]);
                layout.Add(((Region)r, -1));
            }
        mesh.subMeshCount = Mathf.Max(1, lists.Count);
        for (int s = 0; s < lists.Count; s++)
            mesh.SetTriangles(lists[s], s, false);
        mesh.RecalculateBounds();

        if (src.blendShapeCount > 0)
        {
            var dv = new Vector3[count];
            var dn = new Vector3[count];
            var dt = new Vector3[count];
            for (int s = 0; s < src.blendShapeCount; s++)
                for (int k = 0; k < src.GetBlendShapeFrameCount(s); k++)
                {
                    src.GetBlendShapeFrameVertices(s, k, dv, dn, dt);
                    mesh.AddBlendShapeFrame(src.GetBlendShapeName(s), src.GetBlendShapeFrameWeight(s, k),
                        origin.Select(i => dv[i]).ToArray(), origin.Select(i => dn[i]).ToArray(), origin.Select(i => dt[i]).ToArray());
                }
        }
        return mesh;
    }

    private static float[] Support(Frame f, IEnumerable<Vector3> points, float cx, float cz, Func<Vector3, bool> keep, float fallback)
    {
        var sin = new float[Ring];
        var cos = new float[Ring];
        for (int j = 0; j < Ring; j++)
        {
            float a = Angle(j);
            sin[j] = Mathf.Sin(a);
            cos[j] = Mathf.Cos(a);
        }
        var r = new float[Ring];
        for (int j = 0; j < Ring; j++)
            r[j] = float.MinValue;
        foreach (Vector3 p in points)
        {
            if (!keep(p))
                continue;
            float px = f.X(p) - cx, pz = f.Z(p) - cz;
            for (int j = 0; j < Ring; j++)
            {
                float h = px * sin[j] + pz * cos[j];
                if (h > r[j])
                    r[j] = h;
            }
        }
        for (int j = 0; j < Ring; j++)
            r[j] = r[j] == float.MinValue ? fallback : Mathf.Max(r[j], fallback * 0.5f);
        var smooth = new float[Ring];
        for (int j = 0; j < Ring; j++)
            smooth[j] = Mathf.Max(r[j], (r[(j + Ring - 1) % Ring] + 2f * r[j] + r[(j + 1) % Ring]) * 0.25f);
        return smooth;
    }

    private static float Angle(int j) => ((float)j / Ring - 0.5f) * 2f * Mathf.PI;

    private static int Bone(Part p, string name) => string.IsNullOrEmpty(name) ? -1 : Array.IndexOf(p.bones, name);

    private static BoneWeight W(params (int bone, float weight)[] items)
    {
        var list = items.Where(x => x.bone >= 0 && x.weight > 0f).GroupBy(x => x.bone)
            .Select(g => (bone: g.Key, weight: g.Sum(x => x.weight))).OrderByDescending(x => x.weight).Take(4).ToList();
        var w = new BoneWeight();
        float sum = list.Sum(x => x.weight);
        if (list.Count == 0 || sum <= 0f)
        {
            w.weight0 = 1f;
            return w;
        }
        for (int k = 0; k < list.Count; k++)
        {
            float value = list[k].weight / sum;
            switch (k)
            {
                case 0: w.boneIndex0 = list[k].bone; w.weight0 = value; break;
                case 1: w.boneIndex1 = list[k].bone; w.weight1 = value; break;
                case 2: w.boneIndex2 = list[k].bone; w.weight2 = value; break;
                default: w.boneIndex3 = list[k].bone; w.weight3 = value; break;
            }
        }
        return w;
    }

    private static bool BuildCoat(Frame f, List<Part> parts, Part host, Material material, string path, int layer)
    {
        const int K = 7;
        int pelvis = Bone(host, f.pelvisName), thighL = Bone(host, f.hipName[0]), thighR = Bone(host, f.hipName[1]);
        if (pelvis < 0)
            return false;
        float topY = f.yWaist - 0.03f * f.unit, bottomY = f.yKnee - 0.13f * f.unit;
        float cx = f.X(f.pelvis), cz = f.Z(f.pelvis);
        var points = new List<Vector3>();
        foreach (Part p in parts)
            if (p.kind == Kind.Garment || p.kind == Kind.Body)
                for (int i = 0; i < p.vertices.Length; i++)
                    if (p.group[i] == Grp.Torso || p.group[i] == Grp.Thigh || p.group[i] == Grp.Calf)
                        points.Add(p.vertices[i]);
        var ring = new Vector3[K, Ring];
        var weights = new BoneWeight[K, Ring];
        float[] previous = null;
        for (int k = 0; k < K; k++)
        {
            float s = k / (K - 1f);
            float y = Mathf.Lerp(topY, bottomY, s);
            float band = 0.04f * f.unit;
            float[] r = Support(f, points, cx, cz, p => Mathf.Abs(f.Y(p) - y) < band, 0.2f * f.unit);
            float margin = (0.012f + 0.025f * s) * f.unit;
            float widest = r.Max();
            for (int j = 0; j < Ring; j++)
            {
                r[j] = Mathf.Min(Mathf.Max(r[j], widest * 0.7f), 0.32f * f.unit) + margin;
                if (previous != null)
                    r[j] = Mathf.Max(r[j], previous[j] + 0.004f * f.unit);
                float a = Angle(j);
                ring[k, j] = f.At(cx, y, cz) + f.Dir(a) * r[j];
                float legs = 0.7f * s;
                float left = Mathf.Clamp01(0.5f - 0.6f * Mathf.Sin(a));
                weights[k, j] = W((pelvis, 1f - legs), (thighL, legs * left), (thighR, legs * (1f - left)));
            }
            previous = r;
        }
        return Tube(host, f, Prefix + "Coat", path, ring, weights, material, layer);
    }

    private static bool BuildCollar(Frame f, List<Part> parts, Part body, Material material, string path, int layer)
    {
        int chest = Bone(body, f.chestName), neck = Bone(body, f.neckName), head = Bone(body, f.headName);
        if (chest < 0 || neck < 0)
            return false;
        float y0 = f.garmentTop - 0.012f * f.unit;
        float y1 = Mathf.Max(y0 + 0.05f * f.unit, f.yNeck + 0.035f * f.unit);
        Vector3 axis = (f.head - f.neck).normalized;
        float rise = Mathf.Max(Vector3.Dot(axis, f.up), 0.2f);
        Vector3 Centre(float y) => f.neck + axis * ((y - f.yNeck) / rise);

        List<Vector3> garment = parts.Where(p => p.kind == Kind.Garment).SelectMany(p => p.vertices).ToList();
        List<Vector3> skin = body.vertices.ToList();
        float[] Rim(List<Vector3> points, float y, float reach, float pad, float fallback)
        {
            Vector3 c = Centre(y);
            float cx = f.X(c), cz = f.Z(c);
            float[] r = Support(f, points, cx, cz, p =>
            {
                if (Mathf.Abs(f.Y(p) - y) > 0.02f * f.unit)
                    return false;
                float dx = f.X(p) - cx, dz = f.Z(p) - cz;
                return dx * dx + dz * dz < reach * reach;
            }, fallback);
            for (int j = 0; j < Ring; j++)
                r[j] += pad;
            return r;
        }

        float[] bottom = Rim(garment, y0, 0.11f * f.unit, 0.004f * f.unit, 0.085f * f.unit);
        float[] top = Rim(skin, y1, 0.09f * f.unit, 0.006f * f.unit, 0.065f * f.unit);
        var heights = new[] { y0, Mathf.Lerp(y0, y1, 0.45f), y1 };
        var ring = new Vector3[3, Ring];
        var weights = new BoneWeight[3, Ring];
        for (int k = 0; k < 3; k++)
        {
            Vector3 c = Centre(heights[k]);
            float cx = f.X(c), cz = f.Z(c);
            for (int j = 0; j < Ring; j++)
            {
                float r = k == 0 ? bottom[j] : k == 2 ? top[j] : Mathf.Lerp(bottom[j], top[j], 0.6f);
                ring[k, j] = f.At(cx, heights[k], cz) + f.Dir(Angle(j)) * r;
                weights[k, j] = k == 0 ? W((chest, 1f)) : k == 1 ? W((chest, 0.5f), (neck, 0.5f)) : W((neck, 0.85f), (head, 0.15f));
            }
        }
        return Tube(body, f, Prefix + "Collar", path, ring, weights, material, layer);
    }

    private static bool Tube(Part host, Frame f, string objectName, string path, Vector3[,] ring, BoneWeight[,] weights, Material material, int layer)
    {
        int rings = ring.GetLength(0), n = ring.GetLength(1);
        Matrix4x4 toLocal = host.toRef.inverse;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var bws = new List<BoneWeight>();
        for (int k = 0; k < rings; k++)
            for (int j = 0; j <= n; j++)
            {
                verts.Add(toLocal.MultiplyPoint3x4(ring[k, j % n]));
                uvs.Add(new Vector2((float)j / n, rings > 1 ? (float)k / (rings - 1) : 0f));
                bws.Add(weights[k, j % n]);
            }
        var tris = new List<int>();
        for (int k = 0; k < rings - 1; k++)
            for (int j = 0; j < n; j++)
            {
                int a = k * (n + 1) + j, b = a + 1, c = a + n + 2, d = a + n + 1;
                tris.AddRange(new[] { a, d, c, a, c, b });
            }
        var mesh = new Mesh { name = objectName.Substring(Prefix.Length) };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.boneWeights = bws.ToArray();
        mesh.bindposes = host.source.bindposes;
        Outward(mesh);
        mesh.RecalculateBounds();
        mesh = SaveMesh(mesh, path);

        var go = new GameObject(objectName) { layer = layer };
        Transform t = go.transform;
        Transform like = host.renderer.transform;
        t.SetParent(like.parent, false);
        t.localPosition = like.localPosition;
        t.localRotation = like.localRotation;
        t.localScale = like.localScale;
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.bones = host.renderer.bones;
        smr.rootBone = host.renderer.rootBone;
        smr.sharedMesh = mesh;
        smr.sharedMaterial = material;
        smr.updateWhenOffscreen = true;
        return true;
    }

    private static void Outward(Mesh mesh)
    {
        mesh.RecalculateNormals();
        Vector3[] v = mesh.vertices;
        Vector3[] n = mesh.normals;
        if (v.Length == 0)
            return;
        Vector3 centre = Vector3.zero;
        foreach (Vector3 p in v)
            centre += p;
        centre /= v.Length;
        int outside = 0;
        for (int i = 0; i < v.Length; i++)
            if (Vector3.Dot(n[i], v[i] - centre) > 0f)
                outside++;
        if (outside * 2 >= v.Length)
            return;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] tris = mesh.GetTriangles(s);
            for (int k = 0; k + 2 < tris.Length; k += 3)
            {
                int tmp = tris[k + 1];
                tris[k + 1] = tris[k + 2];
                tris[k + 2] = tmp;
            }
            mesh.SetTriangles(tris, s);
        }
        mesh.RecalculateNormals();
    }

    private static bool BuildHat(Outfit o, Frame f, List<Part> parts, Part body, Material felt, Material trim, string path, int layer)
    {
        int headIndex = Bone(body, f.headName);
        if (headIndex < 0)
            return false;
        Transform[] bones = body.renderer.bones;
        Transform headBone = headIndex < bones.Length ? bones[headIndex] : null;
        if (headBone == null)
            return false;

        var head = new List<Vector3>();
        foreach (Part p in parts)
            if (p.kind == Kind.Body || p.kind == Kind.Other)
                for (int i = 0; i < p.vertices.Length; i++)
                    if (p.group[i] == Grp.Head)
                        head.Add(p.vertices[i]);
        if (head.Count == 0)
            return false;
        float u = f.unit;
        float top = head.Max(p => f.Y(p));
        List<Vector3> crown = head.Where(p => f.Y(p) > top - 0.1f * u).ToList();
        float cx = crown.Average(p => f.X(p)), cz = crown.Average(p => f.Z(p));
        float band = top - 0.085f * u;
        float[] r = Support(f, head, cx, cz, p => f.Y(p) > band - 0.015f * u, 0.09f * u);
        for (int j = 0; j < Ring; j++)
            r[j] += 0.006f * u;

        var verts = new List<Vector3>();
        var feltTris = new List<int>();
        var trimTris = new List<int>();

        int RingAt(float y, Func<int, float> radius, Func<int, float> lift = null, float shift = 0f)
        {
            int start = verts.Count;
            for (int j = 0; j < Ring; j++)
            {
                float a = Angle(j);
                float yy = y + (lift != null ? lift(j) : 0f);
                verts.Add(f.At(cx + shift, yy, cz) + f.Dir(a) * radius(j));
            }
            return start;
        }

        void Connect(int a, int b, List<int> tris)
        {
            for (int j = 0; j < Ring; j++)
            {
                int j1 = (j + 1) % Ring;
                tris.AddRange(new[] { a + j, b + j, b + j1, a + j, b + j1, a + j1 });
            }
        }

        void Cap(int ring, Vector3 centre, List<int> tris)
        {
            int c = verts.Count;
            verts.Add(centre);
            for (int j = 0; j < Ring; j++)
                tris.AddRange(new[] { ring + j, c, ring + (j + 1) % Ring });
        }

        switch (o.hat)
        {
            case Hat.Kepi:
            {
                float y0 = band, y1 = top + 0.03f * u;
                int a = RingAt(y0, j => r[j]);
                int b = RingAt(y1, j => r[j] * 1.03f);
                Connect(a, b, feltTris);
                Cap(b, f.At(cx, y1, cz), feltTris);
                int c = RingAt(y0 - 0.001f * u, j => r[j] + 0.003f * u);
                int d = RingAt(y0 + 0.016f * u, j => r[j] + 0.003f * u);
                Connect(c, d, trimTris);
                var front = Enumerable.Range(0, Ring).Where(j => Mathf.Cos(Angle(j)) > 0.17f).ToList();
                int inner = verts.Count;
                foreach (int j in front)
                    verts.Add(f.At(cx, y0 + 0.002f * u, cz) + f.Dir(Angle(j)) * (r[j] + 0.003f * u));
                int outer = verts.Count;
                foreach (int j in front)
                {
                    float depth = Mathf.Sqrt(Mathf.Max(0f, Mathf.Cos(Angle(j))));
                    verts.Add(f.At(cx, y0 + 0.002f * u - 0.022f * u * depth, cz) + f.Dir(Angle(j)) * (r[j] + 0.003f * u + 0.055f * u * depth));
                }
                for (int k = 0; k + 1 < front.Count; k++)
                    trimTris.AddRange(new[] { inner + k, outer + k, outer + k + 1, inner + k, outer + k + 1, inner + k + 1 });
                break;
            }
            case Hat.Bowler:
            {
                float y0 = band, height = top + 0.04f * u - band;
                float[] steps = { 0f, 0.2f, 0.4f, 0.55f, 0.7f, 0.82f, 0.92f };
                int last = -1;
                foreach (float s in steps)
                {
                    float scale = s <= 0.45f ? 1.04f : 1.04f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((s - 0.45f) / 0.55f, 2f)));
                    int ringIndex = RingAt(y0 + s * height, j => r[j] * scale);
                    if (last >= 0)
                        Connect(last, ringIndex, feltTris);
                    last = ringIndex;
                }
                Cap(last, f.At(cx, y0 + height, cz), feltTris);
                int inner = RingAt(y0 + 0.002f * u, j => r[j] * 1.02f);
                int outer = RingAt(y0 + 0.002f * u, j => r[j] * 1.02f + 0.045f * u, j =>
                {
                    float a = Angle(j);
                    return 0.016f * u * Mathf.Pow(Mathf.Sin(a), 2f) - 0.004f * u * Mathf.Pow(Mathf.Cos(a), 2f);
                });
                Connect(inner, outer, feltTris);
                int b0 = RingAt(y0 + 0.003f * u, j => r[j] * 1.05f + 0.002f * u);
                int b1 = RingAt(y0 + 0.024f * u, j => r[j] * 1.05f + 0.002f * u);
                Connect(b0, b1, trimTris);
                break;
            }
            case Hat.Beret:
            {
                float y0 = band + 0.01f * u, height = top + 0.03f * u - y0;
                float[] heights = { 0f, 0.3f, 0.62f, 0.88f };
                float[] scales = { 1.02f, 1.22f, 1.15f, 0.72f };
                int last = -1;
                for (int k = 0; k < heights.Length; k++)
                {
                    float scale = scales[k];
                    int ringIndex = RingAt(y0 + heights[k] * height, j => r[j] * scale, null, 0.025f * u * heights[k]);
                    if (last >= 0)
                        Connect(last, ringIndex, feltTris);
                    last = ringIndex;
                }
                Cap(last, f.At(cx + 0.025f * u, y0 + height, cz), feltTris);
                int b0 = RingAt(y0 - 0.002f * u, j => r[j] * 1.03f);
                int b1 = RingAt(y0 + 0.008f * u, j => r[j] * 1.05f);
                Connect(b0, b1, trimTris);
                break;
            }
            default:
                return false;
        }

        Matrix4x4 toBone = body.bind[headIndex] * body.toRef.inverse;
        var mesh = new Mesh { name = o.role + " Hat" };
        mesh.SetVertices(verts.Select(v => toBone.MultiplyPoint3x4(v)).ToList());
        mesh.subMeshCount = 2;
        mesh.SetTriangles(feltTris, 0);
        mesh.SetTriangles(trimTris, 1);
        Outward(mesh);
        mesh.RecalculateBounds();
        mesh = SaveMesh(mesh, path);

        var go = new GameObject(Prefix + "Hat") { layer = layer };
        go.transform.SetParent(headBone, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { felt, trim };
        return true;
    }

    private static Texture2D Paint(int w, int h, Func<float, float, int, int, Color> paint)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                Color c = paint((i + 0.5f) / w, (j + 0.5f) / h, i, j);
                c.a = 1f;
                pixels[j * w + i] = c;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xffffff) / 16777215f;
        }
    }

    private static float Noise(float x, float y, int seed)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    private static Color Cloth(Color c, int i, int j, int seed)
    {
        float n = 0.9f + 0.14f * Noise(i / 7f, j / 7f, seed) + 0.07f * (Hash(i, j, seed) - 0.5f) + ((i + j) % 2 == 0 ? 0.015f : -0.015f);
        return new Color(c.r * n, c.g * n, c.b * n, 1f);
    }

    private static Color Fold(Color shirt, float t, int i, int j) => Cloth(shirt * (Mathf.Repeat(t * 70f, 1f) < 0.3f ? 0.78f : 0.92f), i, j, 1);

    private static bool Buttons(float du, float y, float top, float bottom, float step, float radius, float unit, float offset)
    {
        if (step <= 0f || y > top + radius * unit * 2f || y < bottom - radius * unit * 2f)
            return false;
        float k = Mathf.Round((top - y) / step);
        float by = top - k * step;
        if (k < 0f || by < bottom - radius * unit)
            return false;
        float dy = (y - by) / unit;
        float dx = (offset > 0f ? Mathf.Abs(du) - offset : du) * Circumference;
        return dx * dx + dy * dy < radius * radius;
    }

    private static bool Star(float x, float y, float r)
    {
        float d = Mathf.Sqrt(x * x + y * y);
        if (d > r)
            return false;
        float a = Mathf.Atan2(y, x) - Mathf.PI / 2f;
        return d < r * (0.42f + 0.58f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * a)), 4f));
    }

    private static Color Torso(Outfit o, Frame f, float u, float v, int i, int j)
    {
        float y = Mathf.LerpUnclamped(f.torsoLo, f.torsoHi, v);
        float m = f.unit;
        float du = u - 0.5f, ax = Mathf.Abs(du);
        float collar = Mathf.Min(f.yNeck - 0.015f * m, f.garmentTop - 0.03f * m);
        bool vest = o.waistcoat.a > 0f;
        float yV = Mathf.Lerp(f.yChest, collar, vest ? 0.3f : 0.4f);
        float span = Mathf.Max(collar - yV, 1e-4f);

        if (vest)
        {
            float bottom = f.yWaist - 0.05f * m - 0.035f * m * Mathf.Clamp01(1f - ax / 0.08f);
            if (y < bottom)
                return Cloth(o.trousers, i, j, 3);
            if (y >= collar)
                return Cloth(o.shirt, i, j, 1);
            if (y > yV && ax < 0.012f + 0.07f * (y - yV) / span)
                return Cloth(o.shirt, i, j, 1);
            if (Buttons(du, y, yV - 0.025f * m, bottom + 0.025f * m, 0.045f * m, 0.0055f, m, 0f))
                return o.buttons;
            Color w = ax > 0.3f ? o.waistcoat * 1.15f : o.waistcoat;
            if (ax < 0.0035f)
                w *= 0.7f;
            return Cloth(w, i, j, 2);
        }

        if (y < f.yCrotch - 0.02f * m)
            return Cloth(o.trousers, i, j, 3);
        if (y >= collar)
            return o.lapels && ax < 0.17f ? Cloth(o.shirt, i, j, 1) : Cloth(o.jacket * 0.9f, i, j, 2);
        if (o.lapels && y > yV)
        {
            float k = (y - yV) / span;
            float half = 0.015f + 0.075f * k;
            if (ax < half)
                return o.tie.a > 0f && ax < 0.011f + 0.006f * k ? Cloth(o.tie, i, j, 4) : Cloth(o.shirt, i, j, 1);
            if (ax < half + 0.014f)
                return Cloth(o.jacket * 0.8f, i, j, 2);
        }
        if (o.belt.a > 0f)
        {
            float above = (y - f.yWaist) / m;
            if (Mathf.Abs(above) < 0.022f)
                return ax < 0.018f ? C(0.55f, 0.50f, 0.36f) : Cloth(o.belt, i, j, 5);
            if (above > 0.022f && above < 0.075f && ax > 0.05f && ax < 0.13f)
                return Cloth(above > 0.068f || ax < 0.056f || ax > 0.124f ? o.belt * 0.7f : o.belt * 1.1f, i, j, 5);
        }
        float first = o.lapels ? yV - 0.03f * m : collar - 0.03f * m;
        float step = o.lapels ? 0.09f * m : (first - (f.yWaist + 0.04f * m)) / 4f;
        float last = o.lapels ? first - step * (o.coat ? 3.2f : 2.2f) : f.yWaist + 0.04f * m;
        if (Buttons(du, y, first, last, step, o.lapels ? 0.009f : 0.008f, m, o.coat ? 0.03f : 0f))
            return o.buttons;
        if (ax < 0.0035f && y < (o.lapels ? yV : collar))
            return Cloth(o.jacket * 0.62f, i, j, 2);
        if (o.pockets)
        {
            if (Mathf.Abs(y - (f.yWaist - 0.07f * m)) / m < 0.005f && ax > 0.07f && ax < 0.13f)
                return Cloth(o.jacket * 0.7f, i, j, 2);
            if (Mathf.Abs(y - Mathf.Lerp(f.yChest, collar, 0.45f)) / m < 0.004f && du < -0.07f && du > -0.12f)
                return Cloth(o.jacket * 0.7f, i, j, 2);
        }
        return Cloth(o.jacket, i, j, 2);
    }

    private static Color Sleeve(Outfit o, Frame f, int s, float end, Color skin, float u, float v, int i, int j)
    {
        float t = v * 1.5f - 0.2f, du = u - 0.5f;
        bool vest = o.waistcoat.a > 0f;
        if (o.roll > 0f)
        {
            if (t > o.roll)
                return skin;
            if (t > o.roll - 0.07f)
                return Fold(o.shirt, t, i, j);
        }
        else if (vest && end > 0f && end < 0.75f && t > end - 0.06f)
        {
            return Fold(o.shirt, t, i, j);
        }
        if (o.armband && s == 0 && t > 0.12f && t < 0.27f)
        {
            float dt = (t - 0.195f) / 0.5f * f.upperArm;
            float dx = du * 0.30f;
            bool cross = Mathf.Abs(dx) < 0.009f && Mathf.Abs(dt) < 0.03f || Mathf.Abs(dx) < 0.03f && Mathf.Abs(dt) < 0.009f;
            return cross ? C(0.68f, 0.08f, 0.08f) : Cloth(C(0.86f, 0.85f, 0.80f), i, j, 6);
        }
        if (vest)
            return Cloth(o.shirt, i, j, 1);
        if (o.lapels && t > 1.0f)
            return Cloth(o.shirt, i, j, 1);
        Color c = !o.lapels && t > 0.86f && t < 0.9f ? o.jacket * 0.82f : o.jacket;
        return Cloth(c, i, j, 2);
    }

    private static Color Leg(Outfit o, float u, float v, int i, int j)
    {
        float t = v * 1.5f - 0.2f, du = u - 0.5f;
        Color c = o.trousers;
        if (o.puttees.a > 0f && t > 0.6f)
        {
            c = o.puttees;
            if (Mathf.Repeat(t * 22f + u * 2f, 1f) < 0.16f)
                c *= 0.75f;
            if (t < 0.625f)
                c *= 0.7f;
        }
        else if (o.lapels && Mathf.Abs(du) < 0.006f)
        {
            c *= 1.1f;
        }
        c = Cloth(c, i, j, 3);
        if (o.mud.a > 0f)
        {
            float amount = Smooth(0.3f, 1.0f, t) * (0.45f + 0.9f * Noise(i / 9f, j / 9f, 11)) * o.mud.a;
            c = Color.Lerp(c, Cloth(o.mud, i, j, 12), Mathf.Clamp01(amount));
        }
        return c;
    }

    private static Color Collar(Outfit o, float u, float v, int i, int j)
    {
        float dx = (Mathf.Abs(u - 0.5f) - 0.07f) * 0.40f, dy = (v - 0.55f) * 0.05f;
        if (o.stars && Star(dx, dy, 0.009f))
            return C(0.90f, 0.90f, 0.88f);
        return Cloth(o.jacket * 0.95f, i, j, 2);
    }

    private static Color Skirt(Outfit o, float u, float v, int i, int j)
    {
        Color c = o.jacket;
        if (Mathf.Abs(u - 0.5f) < 0.004f)
            c *= 0.6f;
        else if ((u < 0.004f || u > 0.996f) && v > 0.5f)
            c *= 0.6f;
        if (v > 0.965f)
            c *= 0.85f;
        return Cloth(c, i, j, 7);
    }

    private static Color SkinColor(Part body)
    {
        Color fallback = C(0.76f, 0.58f, 0.47f);
        Material m = body.renderer.sharedMaterials.FirstOrDefault(x => x != null);
        if (m == null)
            return fallback;
        Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
        Color tint = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
        if (t == null)
            return new Color(tint.r, tint.g, tint.b, 1f);
        RenderTexture rt = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(t, rt);
        RenderTexture.active = rt;
        var read = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Color sum = Color.black;
        int count = 0;
        foreach (Color c in read.GetPixels())
            if (c.r + c.g + c.b > 0.3f)
            {
                sum += c;
                count++;
            }
        Object.DestroyImmediate(read);
        if (count == 0)
            return fallback;
        Color avg = sum / count;
        return new Color(avg.r * tint.r * 0.95f, avg.g * tint.g * 0.95f, avg.b * tint.b * 0.95f, 1f);
    }

    private static Texture2D Save(Texture2D tex, string path, bool wrapU)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool psx = EditorPrefs.GetBool(PsxKey, true);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = !psx;
            importer.filterMode = psx ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapModeU = wrapU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material Mat(string path, Color color, Texture texture, float smoothness, bool twoSided)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(lit);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != lit)
        {
            m.shader = lit;
        }
        if (m.HasProperty("_BaseMap"))
            m.SetTexture("_BaseMap", texture);
        if (m.HasProperty("_MainTex"))
            m.SetTexture("_MainTex", texture);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color"))
            m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness"))
            m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic"))
            m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_Cull"))
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
        m.doubleSidedGI = twoSided;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        existing.Clear();
        existing.ClearBlendShapes();
        existing.name = mesh.name;
        existing.indexFormat = mesh.indexFormat;
        existing.vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (normals.Length == mesh.vertexCount)
            existing.normals = normals;
        Vector4[] tangents = mesh.tangents;
        if (tangents.Length == mesh.vertexCount)
            existing.tangents = tangents;
        existing.uv = mesh.uv;
        BoneWeight[] weights = mesh.boneWeights;
        if (weights.Length == mesh.vertexCount)
            existing.boneWeights = weights;
        existing.bindposes = mesh.bindposes;
        existing.subMeshCount = mesh.subMeshCount;
        for (int s = 0; s < mesh.subMeshCount; s++)
            existing.SetTriangles(mesh.GetTriangles(s), s, false);
        if (mesh.blendShapeCount > 0)
        {
            int count = mesh.vertexCount;
            for (int s = 0; s < mesh.blendShapeCount; s++)
                for (int k = 0; k < mesh.GetBlendShapeFrameCount(s); k++)
                {
                    var dv = new Vector3[count];
                    var dn = new Vector3[count];
                    var dt = new Vector3[count];
                    mesh.GetBlendShapeFrameVertices(s, k, dv, dn, dt);
                    existing.AddBlendShapeFrame(mesh.GetBlendShapeName(s), mesh.GetBlendShapeFrameWeight(s, k), dv, dn, dt);
                }
        }
        existing.RecalculateBounds();
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static string Clean(string name) => System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\- ]+", "_").Trim();

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
