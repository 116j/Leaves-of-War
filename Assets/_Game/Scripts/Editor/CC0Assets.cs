using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class CC0Assets : EditorWindow
{
    private const string ModelRoot = "Assets/_Game/Art/3D/CC0";
    private const string PortraitRoot = "Assets/_Game/Art/3D/Portrait";
    private const string TextureRoot = "Assets/_Game/Art/2D/CC0";
    private const string MeshFolder = ModelRoot + "/_Generated Meshes";
    private const string GreyboxMaterials = "Assets/_Game/Materials/Greybox";
    private const string ModelChild = "CC0 Model";
    private const string Quaternius = "q:trees";
    private const string LocalRoot = "Assets/_Game/Art/3D";
    private const string RealBroom = "ld:Broom";
    private const string RealBroomPage = "https://sketchfab.com/3d-models/broom-3c16a94db7bd4ea8946896824a46b193";
    private const string PsxKey = "LeavesOfWar.CC0.Psx";
    private const string SizeKey = "LeavesOfWar.CC0.Size";

    private enum Fit { Uniform, Stretch, Standing, Aligned }

    private const Fit U = Fit.Uniform;
    private const Fit S = Fit.Stretch;

    private sealed class ModelEntry
    {
        public readonly string level;
        public readonly string target;
        public readonly Fit fit;
        public readonly string[] sources;

        public ModelEntry(string level, string target, Fit fit, params string[] sources)
        {
            this.level = level;
            this.target = target;
            this.fit = fit;
            this.sources = sources;
        }

        public string Leaf => target.TrimEnd('*', ' ').Split('/').Last();
    }

    private sealed class TextureEntry
    {
        public readonly string level;
        public readonly string material;
        public readonly string id;
        public readonly float tile;
        public readonly Color tint;

        public TextureEntry(string level, string material, string id, float tile, Color tint)
        {
            this.level = level;
            this.material = material;
            this.id = id;
            this.tile = tile;
            this.tint = tint;
        }
    }

    private const string L1 = "1 Milano";
    private const string L2 = "2 Parigi";
    private const string L3 = "3 Carso";
    private const string L4 = "4 San Paolo";
    private const string L5 = "5 Roma";
    private const string L6 = "6 Courton";

    private const string GF = "pt:Gothic Furniture/Prefabs/";
    private const string Trees = "pt:TreeBushPackage/Models/";
    private const string Fences = "fences/Fences/";
    private const string Stove = "pt:stove/potbelly_stove_old_stoveclassic_stove.glb";
    private const string Bookshelf = "pt:Bookshelfs/MODEL/bookshelf.fbx";
    private const string Rocks = "pt:Rocks/rocks.fbx";

    private static readonly ModelEntry[] Models =
    {
        new ModelEntry(L1, "Broom_Stick", Fit.Aligned, RealBroom, "pt:GardenTools/Models/Broom.fbx"),
        new ModelEntry(L1, "Broom (pickup)/Model", Fit.Aligned, RealBroom, "pt:GardenTools/Models/Broom.fbx"),
        new ModelEntry(L1, "Iron Bed", S, GF + "Bed.prefab"),
        new ModelEntry(L1, "Walnut Table", S, "ph:WoodenTable_01", GF + "DinnerTable.prefab"),
        new ModelEntry(L1, "Walnut Table/French Grammar Books", S, "ph:book_encyclopedia_set_01"),
        new ModelEntry(L1, "Glass Ink Pot", U, "pt:ink/stoneware_ink_bottle.glb"),
        new ModelEntry(L1, "Brass Oil Lamp", U, "ph:vintage_oil_lamp"),
        new ModelEntry(L1, "Bentwood Chair (moves)", U, GF + "Chair.prefab", "ph:WoodenChair_01"),
        new ModelEntry(L1, "Wardrobe (carved cornice)", S, "pt:wardrobe/source/Wardrobe Filled.fbx", "ph:GothicCabinet_01"),
        new ModelEntry(L1, "Marble Washstand/Cabinet", S, GF + "Dresser.prefab", "ph:vintage_cabinet_01"),
        new ModelEntry(L1, "Marble Washstand/Jug", U, "ph:jug_01"),
        new ModelEntry(L1, "Coat Hooks", Fit.Standing, "pt:coat-rack/source/Hanger.fbx"),

        new ModelEntry(L2, "Bed *", S, GF + "Bed.prefab"),
        new ModelEntry(L2, "Washstand/Stand", S, GF + "SideTable.prefab", "ph:GothicCommode_01"),
        new ModelEntry(L2, "Washstand/Jug", U, "ph:jug_01"),
        new ModelEntry(L2, "Small Desk", S, "pt:Pedestal Desk/Pedestal desk.fbx", "ph:WoodenTable_03"),
        new ModelEntry(L2, "Brass Candlestick", U, "pt:candlestick/candle_stick.glb", "ph:brass_candleholders"),
        new ModelEntry(L2, "Straw Chair", U, "ph:painted_wooden_chair_02", GF + "Chair.prefab"),
        new ModelEntry(L2, "Shelf/Board", S, "ph:Shelf_01"),
        new ModelEntry(L2, "Books (vanish as a group)", S, "ph:book_encyclopedia_set_01"),

        new ModelEntry(L3, "Pietro's Lantern", U, "ph:Lantern_01"),
        new ModelEntry(L3, "Medical Crate", S, "ph:wooden_crate_01"),
        new ModelEntry(L3, "Supply Crate", S, "ph:wooden_crate_02"),
        new ModelEntry(L3, "Water Bucket", U, "ph:wooden_bucket_01"),
        new ModelEntry(L3, "Dugout Stove", U, Stove),
        new ModelEntry(L3, "Trench Shovel", U, "pt:GardenTools/Models/Shovel.fbx"),
        new ModelEntry(L3, "Karst Boulder *", U, "ph:boulder_01", Rocks),
        new ModelEntry(L3, "Karst Rocks *", U, Rocks, "ph:rock_moss_set_01"),
        new ModelEntry(L3, "Drystone Wall *", S, "pt:" + Fences + "drystone_wall/drystone_wall.fbx|" + Fences + "drystone_wall/drystone_wall_half.fbx"),
        new ModelEntry(L3, "Wire Post *", U, "pt:" + Fences + "low_wooden_fence/wooden_fence_pole_broken_large.fbx|" + Fences + "low_wooden_fence/wooden_fence_pole.fbx|" + Fences + "low_wooden_fence/wooden_fence_pole_broken_small.fbx"),
        new ModelEntry(L3, "Dead Tree", U, Trees + "bare_tree.fbx", "ph:dead_tree_trunk_02"),

        new ModelEntry(L4, "Sofa", S, "ph:sofa_03"),
        new ModelEntry(L4, "Coffee Table", S, "ph:CoffeeTable_01"),
        new ModelEntry(L4, "Armchair", U, "ph:ArmChair_01"),
        new ModelEntry(L4, "Rocking Chair", U, "ph:Rockingchair_01"),
        new ModelEntry(L4, "Bookcase", S, Bookshelf, "ph:wooden_bookshelf_worn"),
        new ModelEntry(L4, "Sideboard", S, "ph:ClassicConsole_01", GF + "Dresser.prefab"),
        new ModelEntry(L4, "Gramophone", U, "pt:gramophone/vintage_gramophone.glb"),
        new ModelEntry(L4, "Vase", U, "ph:ceramic_vase_01"),
        new ModelEntry(L4, "Clock", U, "ph:alarm_clock_01"),
        new ModelEntry(L4, "Potted Plant *", U, "ph:potted_plant_01"),
        new ModelEntry(L4, "2 - Wooden Toy", U, "ph:horse_statue_01"),
        new ModelEntry(L4, "3 - Small Chair", U, "ph:painted_wooden_chair_02", GF + "Chair.prefab"),
        new ModelEntry(L4, "Garden Shrub *", U, Trees + "green_bush.fbx|TreeBushPackage/Models/orange_bush.fbx", "ph:shrub_01"),
        new ModelEntry(L4, "Garden Fern *", U, "ph:fern_02"),
        new ModelEntry(L4, "Garden Tree", U, Trees + "green_tree.fbx", "ph:island_tree_01"),

        new ModelEntry(L5, "Writing Desk", S, "pt:Desk/Desk.fbx", "ph:WoodenTable_03"),
        new ModelEntry(L5, "Desk Chair", U, "ph:WoodenChair_01", GF + "Chair.prefab"),
        new ModelEntry(L5, "Oil Lamp", U, "ph:vintage_oil_lamp"),
        new ModelEntry(L5, "Pocket Watch", U, "pt:pocket-watch/pocket_watch.glb", "ph:vintage_pocket_watch"),
        new ModelEntry(L5, "Ink Bottle", U, "pt:ink/ink_bottle_with_quill.glb"),
        new ModelEntry(L5, "Study Bookcase *", S, Bookshelf, "ph:wooden_bookshelf_worn"),
        new ModelEntry(L5, "Book Stack *", S, "ph:book_encyclopedia_set_01"),
        new ModelEntry(L5, "Reading Armchair", U, "ph:ArmChair_01"),
        new ModelEntry(L5, "Drawer Cabinet", S, "ph:drawer_cabinet", GF + "Dresser.prefab"),
        new ModelEntry(L5, "Stove", U, Stove),
        new ModelEntry(L5, "Curtains", S, "pt:curtains/victorian_curtains.glb"),

        new ModelEntry(L6, "Forest Tree *", U, Trees + "green_tree.fbx", Quaternius),
        new ModelEntry(L6, "Leaf Tree *", U, Trees + "bare_tree.fbx", Quaternius),
        new ModelEntry(L6, "Fern *", U, "ph:fern_02"),
        new ModelEntry(L6, "Shrub *", U, Trees + "green_bush.fbx", "ph:shrub_02"),
        new ModelEntry(L6, "Stump *", U, "ph:tree_stump_01"),
        new ModelEntry(L6, "Roots", U, "ph:root_cluster_01"),
        new ModelEntry(L6, "Fallen Trunk", U, "ph:dead_tree_trunk"),
        new ModelEntry(L6, "Mossy Rocks *", U, Rocks, "ph:rock_moss_set_02"),
        new ModelEntry(L6, "Leaf Litter *", S, "pt:leaves-debris/realistic_hd_red_oak_leaf_litter_2036.glb"),
        new ModelEntry(L6, "Camp Crate *", S, "ph:wooden_crate_01"),
        new ModelEntry(L6, "Camp Barrel", U, "ph:wine_barrel_01"),
        new ModelEntry(L6, "Camp Fence *", S, "pt:" + Fences + "low_wooden_fence/wooden_fence_closed.fbx"),
        new ModelEntry(L6, "Lucien's Bucket *", U, "ph:wooden_bucket_02"),
        new ModelEntry(L6, "Lucien's Scythe", U, "pt:GardenTools/Models/Scythe.fbx"),
    };

    private static readonly Dictionary<string, string> PortraitOrigin = new Dictionary<string, string>
    {
        { "Gothic Furniture", "Assets/Art/objects/Gothic Furniture" },
        { "wardrobe", "Assets/Art/objects/wardrobe" },
        { "coat-rack", "Assets/Art/objects/coat-rack" },
        { "ink", "Assets/Art/objects/ink" },
        { "Pedestal Desk", "Assets/Art/objects/Pedestal Desk" },
        { "Desk", "Assets/Art/objects/Desk" },
        { "candlestick", "Assets/Art/objects/candlestick" },
        { "stove", "Assets/Art/objects/stove" },
        { "GardenTools", "Assets/Art/Props/GardenTools" },
        { "Rocks", "Assets/Resources/prefabs/Rocks" },
        { "fences", "Assets/Art/objects/fences" },
        { "TreeBushPackage", "Assets/Art/Environment/Garden/TreeBushPackage" },
        { "Bookshelfs", "Assets/Art/objects/Bookshelfs" },
        { "gramophone", "Assets/Art/objects/gramophone" },
        { "pocket-watch", "Assets/Art/objects/pocket-watch" },
        { "curtains", "Assets/Art/objects/curtains" },
        { "leaves-debris", "Assets/Art/objects/leaves-debris" },
    };


    private static readonly TextureEntry[] Textures =
    {
        new TextureEntry(L1, "Floor Planks", "old_wood_floor", 1.5f, Color.white),
        new TextureEntry(L1, "Plaster", "painted_plaster_wall", 2.5f, new Color(1f, 0.93f, 0.8f)),
        new TextureEntry(L1, "Stencil Border", "patterned_plaster_wall", 1f, new Color(0.8f, 0.62f, 0.52f)),
        new TextureEntry(L1, "Walnut", "wood_table_worn", 1f, new Color(0.8f, 0.6f, 0.45f)),
        new TextureEntry(L1, "Dark Wood", "wood_table_worn", 1f, new Color(0.5f, 0.36f, 0.27f)),
        new TextureEntry(L1, "Marble", "marble_01", 1f, Color.white),
        new TextureEntry(L1, "Wool Blanket", "caban", 0.6f, new Color(0.8f, 0.38f, 0.32f)),
        new TextureEntry(L1, "Assets/_Game/Materials/Props/Diary_Cover.mat", "brown_leather", 0.35f, new Color(1f, 0.86f, 0.7f)),
        new TextureEntry(L2, "Ochre Wallpaper", "patterned_plaster_wall", 1.2f, new Color(1f, 0.8f, 0.45f)),
        new TextureEntry(L2, "Grey Blanket", "caban", 0.6f, new Color(0.85f, 0.83f, 0.8f)),
        new TextureEntry(L2, "Dark Coat", "caban", 0.5f, new Color(0.35f, 0.33f, 0.36f)),
        new TextureEntry(L3, "Trench Earth", "red_dirt_mud_01", 2f, Color.white),
        new TextureEntry(L3, "Limestone", "rock_face_03", 3f, new Color(0.95f, 0.93f, 0.88f)),
        new TextureEntry(L3, "Karst Ground", "rocky_terrain", 4f, Color.white),
        new TextureEntry(L3, "Trench Planks", "weathered_planks", 2f, new Color(0.85f, 0.8f, 0.75f)),
        new TextureEntry(L3, "Sandbag", "caban", 0.5f, new Color(0.85f, 0.78f, 0.58f)),
        new TextureEntry(L4, "Terrazzo", "terrazzo_tiles", 1f, Color.white),
        new TextureEntry(L4, "Lime Wall", "beige_wall_001", 2.5f, Color.white),
        new TextureEntry(L4, "Garden Soil", "forest_floor", 3f, Color.white),
        new TextureEntry(L4, "Garden Wall", "plastered_wall", 2.5f, new Color(1f, 0.95f, 0.85f)),
        new TextureEntry(L5, "Parquet", "herringbone_parquet", 1.5f, Color.white),
        new TextureEntry(L5, "Worn Plaster", "worn_plaster_wall", 2.5f, Color.white),
        new TextureEntry(L5, "Shutter Wood", "weathered_brown_planks", 1f, new Color(0.6f, 0.72f, 0.55f)),
        new TextureEntry(L6, "Forest Floor", "forest_floor", 3f, Color.white),
        new TextureEntry(L6, "Forest Mud", "mud_forest", 3f, Color.white),
        new TextureEntry(L6, "Tent Canvas", "caban", 1f, new Color(0.75f, 0.72f, 0.55f)),
    };

    private const string ViewRoot = "Assets/_Game/Art/2D/Views";

    private sealed class ViewEntry
    {
        public readonly string level;
        public readonly string material;
        public readonly string file;
        public readonly string url;
        public readonly string page;
        public readonly Color tint;

        public ViewEntry(string level, string material, string file, string url, string page, Color tint)
        {
            this.level = level;
            this.material = material;
            this.file = file;
            this.url = url;
            this.page = page;
            this.tint = tint;
        }

        public string Path => ViewRoot + "/" + file;
    }

    private static readonly ViewEntry[] Views =
    {
        new ViewEntry(L1, "Window View", "milano_view.jpg",
            "https://images.pexels.com/photos/35973349/pexels-photo-35973349.jpeg?auto=compress&cs=tinysrgb&w=1920",
            "https://www.pexels.com/photo/historic-courtyard-in-san-fernando-spain-35973349/",
            new Color(0.92f, 0.9f, 0.86f)),
    };

    private static readonly HashSet<string> KeepNames = new HashSet<string>
    {
        ModelChild, "Exercise Sheets", "Folded Newspaper 1914", "Glass Ink Pot", "Dip Pen", "Marble Top", "Basin", "Bowl",
        "Letters and Envelopes", "Moammed's Coat (vanishes)", "Branch Anchors", "1 - Drawings", "Lantern Light"
    };

    private static readonly string[] Levels = { L1, L2, L3, L4, L5, L6 };

    private Vector2 scroll;

    [MenuItem("Tools/Leaves of War/Level Assets (CC0 + Portrait)")]
    private static void OpenWindow()
    {
        var window = GetWindow<CC0Assets>("Level Assets");
        window.minSize = new Vector2(560f, 420f);
    }

    private static bool Psx
    {
        get => EditorPrefs.GetBool(PsxKey, true);
        set => EditorPrefs.SetBool(PsxKey, value);
    }

    private static int PsxSize
    {
        get => EditorPrefs.GetInt(SizeKey, 256);
        set => EditorPrefs.SetInt(SizeKey, value);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Portrait: copy the folder from The Portrait of Hortensia (with its .meta files) into the folder shown by the Folder button.\n" +
            "Poly Haven / Quaternius: Open, download (models FBX 1K, textures 1K JPG), unzip into the Folder.\n" +
            "Then open a level scene and press Apply. The first available source of each object wins; missing ones stay greybox.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        Psx = EditorGUILayout.ToggleLeft("PSX textures for downloads (Point filter, no mipmaps)", Psx, GUILayout.Width(330f));
        PsxSize = EditorGUILayout.IntPopup(PsxSize, new[] { "128", "256", "512", "1024" }, new[] { 128, 256, 512, 1024 }, GUILayout.Width(70f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply to open scene", GUILayout.Height(28f)))
            Apply(false);
        if (GUILayout.Button("Reapply (rebuild models)", GUILayout.Height(28f)))
            Apply(true);
        if (GUILayout.Button("Create all folders", GUILayout.Height(28f)))
            CreateAllFolders();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Download all missing from Poly Haven (models FBX 1K + textures 1K)", GUILayout.Height(28f)))
            DownloadAllMissing();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        var listed = new HashSet<string>();
        foreach (string level in Levels)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Level " + level, EditorStyles.boldLabel);
            foreach (ModelEntry m in Models.Where(m => m.level == level))
                foreach (string source in m.sources)
                    if (listed.Add(SourceFolder(source)))
                        Row(source);
            foreach (TextureEntry t in Textures.Where(t => t.level == level))
                if (listed.Add(TextureFolder(t.id)))
                    TextureRow(t.id);
            foreach (ViewEntry v in Views.Where(v => v.level == level))
                RowGui(AssetDatabase.LoadAssetAtPath<Texture2D>(v.Path) != null, "Window view  " + v.file + "  (Pexels)", "Material: " + v.material, v.page, ViewRoot,
                    () => DownloadView(v));
        }
        EditorGUILayout.EndScrollView();
    }

    private static void Row(string source)
    {
        bool portrait = source.StartsWith("pt:", StringComparison.Ordinal);
        bool local = source.StartsWith("ld:", StringComparison.Ordinal);
        string label = portrait ? "Portrait  " + PortraitTop(source)
            : local ? "Your download  " + source.Substring(3) + "  (Sketchfab, JmPrsh153 - put the FBX/GLB in the Folder)"
            : source.StartsWith("q:", StringComparison.Ordinal) ? "Quaternius  Textured LowPoly Trees" : "Poly Haven  " + source.Substring(3);
        string tip = portrait && PortraitOrigin.TryGetValue(PortraitTop(source), out string origin) ? "Copy from The Portrait: " + origin : null;
        bool ph = source.StartsWith("ph:", StringComparison.Ordinal);
        RowGui(SourceFiles(source).Count > 0, label, tip, portrait ? null : SourceUrl(source), SourceFolder(source),
            ph ? () => Download(new List<string> { source.Substring(3) }, new List<string>()) : (Action)null);
    }

    private static void TextureRow(string id)
    {
        RowGui(FindTexture(TextureFolder(id), "", Diffuse) != null, "Texture  " + id, null, "https://polyhaven.com/a/" + id, TextureFolder(id),
            () => Download(new List<string>(), new List<string> { id }));
    }

    private static void RowGui(bool ok, string label, string tip, string url, string folder, Action get)
    {
        EditorGUILayout.BeginHorizontal();
        var style = new GUIStyle(EditorStyles.label) { normal = { textColor = ok ? new Color(0.35f, 0.75f, 0.35f) : new Color(0.85f, 0.45f, 0.35f) } };
        EditorGUILayout.LabelField(ok ? "✓" : "✗", style, GUILayout.Width(16f));
        EditorGUILayout.LabelField(new GUIContent(label, tip));
        GUI.enabled = get != null;
        if (GUILayout.Button("Get", GUILayout.Width(40f)))
            EditorApplication.delayCall += () => get();
        GUI.enabled = url != null;
        if (GUILayout.Button("Open", GUILayout.Width(50f)))
            Application.OpenURL(url);
        GUI.enabled = true;
        if (GUILayout.Button("Folder", GUILayout.Width(55f)))
        {
            EnsureFolder(folder);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(folder));
            EditorUtility.RevealInFinder(folder);
        }
        EditorGUILayout.EndHorizontal();
    }

    private static HttpClient http;

    private static HttpClient Http
    {
        get
        {
            if (http == null)
            {
                http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("LeavesOfWar-UnityEditor/1.0");
            }
            return http;
        }
    }

    private static void DownloadAllMissing()
    {
        List<string> models = Models.SelectMany(e => e.sources)
            .Where(s => s.StartsWith("ph:", StringComparison.Ordinal))
            .Select(s => s.Substring(3))
            .Distinct()
            .Where(id => ModelFiles(ModelRoot + "/" + id).Count == 0)
            .ToList();
        List<string> textures = Textures.Select(e => e.id)
            .Distinct()
            .Where(id => FindTexture(TextureFolder(id), "", Diffuse) == null)
            .ToList();
        if (models.Count + textures.Count == 0)
        {
            EditorUtility.DisplayDialog("Poly Haven", "Everything is already downloaded.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Poly Haven", "Download " + models.Count + " models and " + textures.Count + " textures from polyhaven.com (CC0)?", "Download", "Cancel"))
            return;
        Download(models, textures);
    }

    private static void Download(List<string> models, List<string> textures)
    {
        int total = models.Count + textures.Count;
        int done = 0;
        var failed = new List<string>();
        try
        {
            foreach (string id in models)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Poly Haven", "Model " + id + "  (" + (done + 1) + "/" + total + ")", done / (float)total))
                    break;
                try { DownloadModel(id); }
                catch (Exception e) { failed.Add(id + ": " + Reason(e)); }
                done++;
            }
            foreach (string id in textures)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Poly Haven", "Texture " + id + "  (" + (done + 1) + "/" + total + ")", done / (float)total))
                    break;
                try { DownloadTexture(id); }
                catch (Exception e) { failed.Add(id + ": " + Reason(e)); }
                done++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
        string message = (done - failed.Count) + " of " + total + " downloaded.";
        if (failed.Count > 0)
            message += "\n\nFailed:\n" + string.Join("\n", failed);
        Debug.Log("Poly Haven\n" + message);
        EditorUtility.DisplayDialog("Poly Haven", message + "\n\nNow open a level and press Apply.", "OK");
    }

    private static void DownloadView(ViewEntry view)
    {
        try
        {
            EditorUtility.DisplayProgressBar("Window view", view.file, 0.5f);
            Save(view.url, view.Path);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Window view", "Download failed: " + Reason(e) + "\n\nDownload it from the page and save it as " + view.Path, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    private static int ApplyViews(Scene scene)
    {
        int count = 0;
        var renderers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToList();
        foreach (ViewEntry view in Views)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(view.Path);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GreyboxMaterials + "/" + view.material + ".mat");
            if (texture == null || material == null)
                continue;

            float imageAspect = texture.height > 0 ? texture.width / (float)texture.height : 1f;
            if (AssetImporter.GetAtPath(view.Path) is TextureImporter importer)
            {
                importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
                if (sourceHeight > 0)
                    imageAspect = sourceWidth / (float)sourceHeight;
                if (importer.npotScale != TextureImporterNPOTScale.None)
                {
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }
                int size = Psx ? Mathf.Max(PsxSize, 512) : 2048;
                FilterMode filter = Psx ? FilterMode.Point : FilterMode.Bilinear;
                if (importer.maxTextureSize != size || importer.filterMode != filter || importer.mipmapEnabled == Psx || importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.maxTextureSize = size;
                    importer.filterMode = filter;
                    importer.mipmapEnabled = !Psx;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
            }

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
                material.shader = unlit;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", view.tint);

            MeshRenderer backdrop = renderers.FirstOrDefault(r => r.sharedMaterial == material);
            float boxAspect = 1f;
            if (backdrop != null)
            {
                if (backdrop.GetComponent<WindowView>() == null)
                    backdrop.gameObject.AddComponent<WindowView>();
                Vector3 size = backdrop.transform.lossyScale;
                float w = Mathf.Abs(size.x), h = Mathf.Abs(size.y);
                if (h > 1e-4f)
                    boxAspect = w / h;
            }
            Vector2 tiling = Vector2.one;
            Vector2 offset = Vector2.zero;
            if (imageAspect > boxAspect)
            {
                tiling.x = boxAspect / imageAspect;
                offset.x = (1f - tiling.x) * 0.5f;
            }
            else
            {
                tiling.y = imageAspect / boxAspect;
                offset.y = (1f - tiling.y) * 0.5f;
            }
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureOffset("_BaseMap", offset);
            EditorUtility.SetDirty(material);
            count++;
        }
        return count;
    }

    private static string Reason(Exception e)
    {
        while (e.InnerException != null)
            e = e.InnerException;
        return e.Message;
    }

    private static string FilesJson(string id) => Http.GetStringAsync("https://api.polyhaven.com/files/" + id).GetAwaiter().GetResult();

    private static void DownloadModel(string id)
    {
        string json = FilesJson(id);
        Match fbx = Regex.Match(json, "\"url\"\\s*:\\s*\"([^\"]+/" + Regex.Escape(id) + "_1k\\.fbx)\"");
        if (!fbx.Success)
            throw new Exception("no FBX 1K on Poly Haven");
        string folder = ModelRoot + "/" + id;
        Save(fbx.Groups[1].Value, folder + "/" + id + "_1k.fbx");
        var seen = new HashSet<string>();
        foreach (Match m in Regex.Matches(json, "\"(textures/[^\"]+_1k\\.(?:jpg|png))\"\\s*:\\s*\\{[^{}]*?\"url\"\\s*:\\s*\"([^\"]+)\""))
            if (seen.Add(m.Groups[1].Value))
                Save(m.Groups[2].Value, folder + "/" + m.Groups[1].Value);
    }

    private static void DownloadTexture(string id)
    {
        string json = FilesJson(id);
        string diffuse = MapUrl(json, "Diffuse");
        if (diffuse == null)
            throw new Exception("no 1K diffuse on Poly Haven");
        string folder = TextureFolder(id);
        Save(diffuse, folder + "/" + Path.GetFileName(new Uri(diffuse).AbsolutePath));
        string normal = MapUrl(json, "nor_gl");
        if (normal != null)
            Save(normal, folder + "/" + Path.GetFileName(new Uri(normal).AbsolutePath));
    }

    private static string MapUrl(string json, string map)
    {
        Match m = Regex.Match(json, "\"" + map + "\"\\s*:\\s*\\{\\s*\"1k\"\\s*:\\s*\\{[^{}]*?\"jpg\"\\s*:\\s*\\{[^{}]*?\"url\"\\s*:\\s*\"([^\"]+)\"");
        if (m.Success)
            return m.Groups[1].Value;
        m = Regex.Match(json, "\"" + map + "\"[\\s\\S]*?\"1k\"[\\s\\S]*?\"jpg\"[\\s\\S]*?\"url\"\\s*:\\s*\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static void Save(string url, string assetPath)
    {
        byte[] data = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        string path = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, data);
    }

    private static string PortraitTop(string source) => source.Substring(3).Split('|')[0].Split('/')[0];

    private static string SourceUrl(string source) =>
        source == Quaternius ? "https://quaternius.itch.io/textured-lowpoly-trees"
        : source == RealBroom ? RealBroomPage
        : "https://polyhaven.com/a/" + source.Substring(3);

    private static string SourceFolder(string source)
    {
        if (source == Quaternius)
            return ModelRoot + "/quaternius_trees";
        if (source.StartsWith("pt:", StringComparison.Ordinal))
            return PortraitRoot + "/" + PortraitTop(source);
        if (source.StartsWith("ld:", StringComparison.Ordinal))
            return LocalRoot + "/" + source.Substring(3);
        return ModelRoot + "/" + source.Substring(3);
    }

    private static string SourceName(string source) =>
        source.StartsWith("pt:", StringComparison.Ordinal) ? "Portrait: " + PortraitTop(source) + (PortraitOrigin.TryGetValue(PortraitTop(source), out string o) ? "  (copy " + o + ")" : "")
        : source.StartsWith("ld:", StringComparison.Ordinal) ? "Your download in " + SourceFolder(source)
        : source == Quaternius ? "Quaternius Textured LowPoly Trees" : "Poly Haven: " + source.Substring(3);

    private static List<string> SourceFiles(string source)
    {
        if (!source.StartsWith("pt:", StringComparison.Ordinal))
            return ModelFiles(SourceFolder(source));
        var files = new List<string>();
        foreach (string part in source.Substring(3).Split('|'))
        {
            string path = PortraitRoot + "/" + part;
            if (AssetDatabase.IsValidFolder(path))
                files.AddRange(ModelFiles(path));
            else if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                files.Add(path);
        }
        return files;
    }

    private static string TextureFolder(string id) => TextureRoot + "/" + id;

    private static void CreateAllFolders()
    {
        foreach (ModelEntry m in Models)
            foreach (string source in m.sources)
                if (!source.StartsWith("pt:", StringComparison.Ordinal))
                    EnsureFolder(SourceFolder(source));
        foreach (TextureEntry t in Textures)
            EnsureFolder(TextureFolder(t.id));
        EnsureFolder(PortraitRoot);
        AssetDatabase.Refresh();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void Apply(bool rebuild)
    {
        Scene scene = SceneManager.GetActiveScene();
        var log = new List<string>();
        PrepareTextures();
        RepairPortraitMaterials();
        int textured = ApplyTextures(scene) + ApplyViews(scene);
        int papers = PaperProps.Apply(scene, Psx);
        int placed = 0, missing = 0;
        var missingSources = new HashSet<string>();
        var counters = new Dictionary<string, int>();

        foreach (ModelEntry entry in Models)
        {
            List<Transform> targets = FindTargets(scene, entry.target);
            if (targets.Count == 0)
                continue;
            string source = null;
            List<string> files = null;
            foreach (string candidate in entry.sources)
            {
                files = SourceFiles(candidate);
                if (files.Count > 0)
                {
                    source = candidate;
                    break;
                }
            }
            if (source == null)
            {
                missing += targets.Count;
                missingSources.Add(SourceName(entry.sources[0]));
                continue;
            }

            foreach (Transform target in targets)
            {
                Transform existing = target.Find(ModelChild);
                if (existing != null)
                {
                    if (!rebuild)
                        continue;
                    Object.DestroyImmediate(existing.gameObject);
                }

                counters.TryGetValue(source, out int index);
                counters[source] = index + 1;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(files[index % files.Count]);
                if (asset != null && Place(target, asset, entry, source))
                    placed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        log.Add(placed + " models placed, " + textured + " greybox materials textured, " + papers + " papers, letters and drawings.");
        if (missingSources.Count > 0)
            log.Add(missing + " placeholders still greybox, missing:\n  " + string.Join("\n  ", missingSources));
        log.Add("\nIf a model faces the wrong way, rotate its \"" + ModelChild + "\" child by 180 (Apply keeps it).");
        Debug.Log("Level Assets\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Level Assets", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static List<Transform> FindTargets(Scene scene, string target)
    {
        string[] parts = target.Split('/');
        bool wildcard = parts[0].EndsWith("*");
        string first = parts[0].TrimEnd('*', ' ');
        var result = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == ModelChild || IsInsideModel(t))
                    continue;
                bool match = wildcard ? t.name.StartsWith(first + " ", StringComparison.Ordinal) : t.name == first;
                if (!match)
                    continue;
                Transform found = parts.Length > 1 ? t.Find(string.Join("/", parts.Skip(1))) : t;
                if (found != null)
                    result.Add(found);
            }
        return result.OrderBy(t => t.name, StringComparer.Ordinal).ToList();
    }

    private static bool IsInsideModel(Transform t)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
            if (p.name == ModelChild)
                return true;
        return false;
    }

    private static readonly string[] ModelExtensions = { ".fbx", ".obj", ".glb", ".gltf", ".prefab" };

    private static List<string> ModelFiles(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return new List<string>();
        return AssetDatabase.FindAssets("", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => ModelExtensions.Any(e => p.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .Where(p => !p.Contains("/_Generated"))
            .Distinct()
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    private static bool Place(Transform target, GameObject asset, ModelEntry entry, string source)
    {
        var parts = new List<Renderer>();
        CollectParts(target, true, parts);
        if (entry.fit == Fit.Aligned)
            return PlaceAligned(target, asset, parts, source);
        Bounds goal;
        if (parts.Count > 0)
        {
            goal = WorldBounds(parts[0]);
            foreach (Renderer r in parts)
                goal.Encapsulate(WorldBounds(r));
        }
        else
        {
            goal = new Bounds(target.position + Vector3.up * 0.5f, Vector3.one);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, target);
        instance.name = ModelChild;
        Transform t = instance.transform;
        Quaternion imported = asset.transform.localRotation;
        Vector3 importedScale = asset.transform.localScale;
        if (Measure(instance, out _) == false)
        {
            Object.DestroyImmediate(instance);
            return false;
        }

        float bestScore = float.MinValue;
        float bestYaw = 0f;
        foreach (float turn in new[] { 0f, 90f })
        {
            Orient(t, target, turn, imported, importedScale);
            Measure(instance, out Bounds b);
            float score = entry.fit == Fit.Stretch
                ? -Mathf.Abs(Mathf.Log(Ratio(b.size.x, b.size.z) / Ratio(goal.size.x, goal.size.z)))
                : Mathf.Min(Ratio(goal.size.x, b.size.x), Ratio(goal.size.y, b.size.y), Ratio(goal.size.z, b.size.z));
            if (score > bestScore)
            {
                bestScore = score;
                bestYaw = turn;
            }
        }

        Orient(t, target, bestYaw, imported, importedScale);
        Measure(instance, out Bounds natural);
        Vector3 f = new Vector3(Ratio(goal.size.x, natural.size.x), Ratio(goal.size.y, natural.size.y), Ratio(goal.size.z, natural.size.z));
        Vector3 scale = t.localScale;
        if (entry.fit == Fit.Standing)
        {
            scale *= Ratio(goal.max.y + 0.1f, natural.size.y);
        }
        else if (entry.fit == Fit.Uniform)
        {
            scale *= Mathf.Min(f.x, f.y, f.z);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = t.TransformDirection(i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
                int j = Dominant(axis);
                scale[i] *= f[j];
            }
        }
        t.localScale = scale;

        Measure(instance, out Bounds placed);
        if (entry.fit == Fit.Standing)
            t.position += new Vector3(goal.center.x - placed.center.x, -placed.min.y, goal.min.z - placed.min.z);
        else
            t.position += new Vector3(goal.center.x - placed.center.x, goal.min.y - placed.min.y, goal.center.z - placed.center.z);

        ApplyModelMaterials(instance, source);
        instance.layer = target.gameObject.layer;
        foreach (Renderer r in parts)
            r.enabled = false;
        return true;
    }

    private static Bounds WorldBounds(Renderer r)
    {
        MeshFilter filter = r.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return r.bounds;
        Bounds local = filter.sharedMesh.bounds;
        Matrix4x4 m = r.transform.localToWorldMatrix;
        var result = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            result.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return result;
    }

    private static void Orient(Transform t, Transform target, float yaw, Quaternion imported, Vector3 importedScale)
    {
        t.localPosition = Vector3.zero;
        t.rotation = Quaternion.Euler(0f, target.eulerAngles.y + yaw, 0f) * imported;
        t.localScale = importedScale;
        Vector3 s = t.localScale;
        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            float world = t.localToWorldMatrix.MultiplyVector(axis).magnitude;
            if (world > 1e-5f)
                s[i] *= Mathf.Abs(importedScale[i]) / world;
        }
        t.localScale = s;
    }

    private static float Ratio(float a, float b) => b > 1e-4f ? a / b : 1f;

    private static int Dominant(Vector3 v)
    {
        v = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        return v.x >= v.y && v.x >= v.z ? 0 : v.y >= v.z ? 1 : 2;
    }

    private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

    private static int Longest(Vector3 v) => v.x >= v.y && v.x >= v.z ? 0 : v.y >= v.z ? 1 : 2;

    private static Bounds FrameBounds(IEnumerable<Renderer> renderers, Matrix4x4 frame)
    {
        bool any = false;
        Bounds result = default;
        foreach (Renderer r in renderers)
        {
            MeshFilter filter = r.GetComponent<MeshFilter>();
            Bounds local;
            Matrix4x4 m;
            if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                local = skinned.sharedMesh.bounds;
                m = frame * r.transform.localToWorldMatrix;
            }
            else if (filter != null && filter.sharedMesh != null)
            {
                local = filter.sharedMesh.bounds;
                m = frame * r.transform.localToWorldMatrix;
            }
            else
                continue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any)
                {
                    result = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                    result.Encapsulate(p);
            }
        }
        return result;
    }

    private static void Normalize(Transform t, Vector3 importedScale, float factor)
    {
        t.localScale = importedScale;
        Vector3 s = importedScale;
        for (int i = 0; i < 3; i++)
        {
            float world = t.localToWorldMatrix.MultiplyVector(Axis(i)).magnitude;
            if (world > 1e-6f)
                s[i] *= Mathf.Abs(importedScale[i]) * factor / world;
        }
        t.localScale = s;
    }

    private static float HeadSign(GameObject model, Matrix4x4 frame, int axis)
    {
        var points = new List<Vector3>();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            try
            {
                Matrix4x4 m = frame * filter.transform.localToWorldMatrix;
                foreach (Vector3 v in filter.sharedMesh.vertices)
                    points.Add(m.MultiplyPoint3x4(v));
            }
            catch (Exception)
            {
                return 0f;
            }
        }
        if (points.Count < 8)
            return 0f;
        float min = points.Min(p => p[axis]);
        float max = points.Max(p => p[axis]);
        float band = (max - min) * 0.2f;
        float Spread(IEnumerable<Vector3> set)
        {
            var list = set.ToList();
            if (list.Count == 0)
                return 0f;
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            return (list.Max(p => p[a]) - list.Min(p => p[a])) * (list.Max(p => p[b]) - list.Min(p => p[b]));
        }
        float top = Spread(points.Where(p => p[axis] > max - band));
        float bottom = Spread(points.Where(p => p[axis] < min + band));
        if (Mathf.Approximately(top, bottom))
            return 0f;
        return top > bottom ? 1f : -1f;
    }

    private static float Inverse(float v) => Mathf.Abs(v) > 1e-6f ? 1f / v : 1f;

    private static Material Steady(Material m, string folder)
    {
        if (m == null || !m.HasProperty("_VertexSnapStrength"))
            return m;
        string fixedFolder = folder + "/_Fixed Materials";
        string path = fixedFolder + "/" + Clean(m.name) + "_steady.mat";
        Material steady = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (steady == null)
        {
            EnsureFolder(fixedFolder);
            steady = new Material(m) { name = m.name + " (steady)" };
            AssetDatabase.CreateAsset(steady, path);
        }
        else
            steady.CopyPropertiesFromMaterial(m);
        steady.SetFloat("_VertexSnapStrength", 0f);
        if (steady.HasProperty("_AffineStrength"))
            steady.SetFloat("_AffineStrength", 0f);
        EditorUtility.SetDirty(steady);
        return steady;
    }

    private static bool PlaceAligned(Transform target, GameObject asset, List<Renderer> parts, string source)
    {
        if (parts.Count == 0)
            return false;
        Matrix4x4 frame = Matrix4x4.TRS(target.position, target.rotation, Vector3.one).inverse;
        Bounds goal = FrameBounds(parts, frame);
        int ga = Longest(goal.size);

        float goalHead = 0f;
        Renderer head = parts.FirstOrDefault(r => { string n = r.name.ToLowerInvariant(); return n.Contains("hair") || n.Contains("brist") || n.Contains("head"); });
        if (head != null)
            goalHead = Mathf.Sign(FrameBounds(new[] { head }, frame).center[ga] - goal.center[ga]);
        else
        {
            Transform tip = target.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.ToLowerInvariant().Contains("tip"));
            if (tip != null)
                goalHead = Mathf.Sign(frame.MultiplyPoint3x4(tip.position)[ga] - goal.center[ga]);
        }

        var pivotObject = new GameObject(ModelChild);
        Transform pivot = pivotObject.transform;
        pivot.SetParent(target, false);
        Vector3 ts = target.localScale;
        pivot.localScale = new Vector3(Inverse(ts.x), Inverse(ts.y), Inverse(ts.z));

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, pivot);
        instance.name = "Model";
        Transform t = instance.transform;
        Vector3 importedScale = asset.transform.localScale;
        t.localPosition = Vector3.zero;
        t.rotation = target.rotation * asset.transform.localRotation;
        Normalize(t, importedScale, 1f);
        Renderer[] own = instance.GetComponentsInChildren<Renderer>(true);
        if (own.Length == 0)
        {
            Object.DestroyImmediate(pivotObject);
            return false;
        }

        Bounds mb = FrameBounds(own, frame);
        int ma = Longest(mb.size);
        if (ma != ga)
        {
            t.rotation = target.rotation * Quaternion.FromToRotation(Axis(ma), Axis(ga)) * Quaternion.Inverse(target.rotation) * t.rotation;
            Normalize(t, importedScale, 1f);
            mb = FrameBounds(own, frame);
        }

        float modelHead = HeadSign(instance, frame, ga);
        if (goalHead != 0f && modelHead != 0f && goalHead != modelHead)
        {
            t.rotation = target.rotation * Quaternion.AngleAxis(180f, Axis((ga + 1) % 3)) * Quaternion.Inverse(target.rotation) * t.rotation;
            Normalize(t, importedScale, 1f);
            mb = FrameBounds(own, frame);
        }

        Normalize(t, importedScale, Ratio(goal.size[ga], mb.size[ga]));
        mb = FrameBounds(own, frame);
        t.position += target.rotation * (goal.center - mb.center);

        int layer = target.gameObject.layer;
        UnityEngine.Rendering.ShadowCastingMode shadows = parts[0].shadowCastingMode;
        foreach (Transform x in pivotObject.GetComponentsInChildren<Transform>(true))
            x.gameObject.layer = layer;
        foreach (Renderer r in own)
            r.shadowCastingMode = shadows;
        ApplyModelMaterials(instance, source);
        foreach (Renderer r in own)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = Steady(mats[i], SourceFolder(source));
            r.sharedMaterials = mats;
        }
        foreach (Renderer r in parts)
            r.enabled = false;
        return true;
    }

    private static bool Measure(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer)
                continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return any;
    }

    private static void CollectParts(Transform t, bool root, List<Renderer> list)
    {
        if (!root && (KeepNames.Contains(t.name) || Models.Any(m => m.Leaf == t.name || (m.target.EndsWith("*") && t.name.StartsWith(m.Leaf + " ", StringComparison.Ordinal)))))
            return;
        foreach (Renderer r in t.GetComponents<Renderer>())
            if (r is MeshRenderer)
                list.Add(r);
        foreach (Transform child in t)
            CollectParts(child, false, list);
    }

    private static readonly string[] Diffuse = { "_diff", "diffuse", "basecolor", "base_color", "albedo", "_col" };
    private static readonly string[] Normal = { "_nor_gl", "normalgl", "_normal", "_nrm" };

    private static Texture2D FindTexture(string folder, string prefix, string[] keys)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return null;
        string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        string want = Clean(prefix);
        string match = null;
        foreach (string p in paths)
        {
            string file = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (file.Contains("_dx") || !keys.Any(k => file.Contains(k)))
                continue;
            if (want.Length > 0 && Clean(file).StartsWith(want))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (match == null)
                match = p;
        }
        return match != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(match) : null;
    }

    private static string Clean(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static void ApplyModelMaterials(GameObject instance, string source)
    {
        bool portrait = source.StartsWith("pt:", StringComparison.Ordinal) || source.StartsWith("ld:", StringComparison.Ordinal);
        string folder = SourceFolder(source);
        string materialFolder = folder + "/Materials";
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material original = mats[i];
                string name = original != null ? original.name.Replace(" (Instance)", "") : r.name;
                Material marker = AssetDatabase.LoadAssetAtPath<Material>(GreyboxMaterials + "/" + name + ".mat");
                if (marker != null && !HasTexture(original))
                {
                    mats[i] = marker;
                    changed = true;
                    continue;
                }
                if (portrait)
                {
                    Material fixedMaterial = RepairMaterial(original, name, folder);
                    if (fixedMaterial != null && fixedMaterial != original)
                    {
                        mats[i] = fixedMaterial;
                        changed = true;
                    }
                    continue;
                }
                string path = materialFolder + "/" + Clean(name) + ".mat";
                Material made = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (made == null)
                {
                    Texture2D diffuse = FindTexture(folder, name, Diffuse);
                    if (diffuse == null)
                        continue;
                    EnsureFolder(materialFolder);
                    made = new Material(LitShader()) { name = name };
                    made.SetTexture("_BaseMap", diffuse);
                    made.SetColor("_BaseColor", Color.white);
                    made.SetFloat("_Smoothness", 0.1f);
                    Texture2D normal = FindTexture(folder, name, Normal);
                    if (normal != null)
                    {
                        made.SetTexture("_BumpMap", normal);
                        made.EnableKeyword("_NORMALMAP");
                    }
                    AssetDatabase.CreateAsset(made, path);
                }
                mats[i] = made;
                changed = true;
            }
            if (changed)
                r.sharedMaterials = mats;
        }
    }

    private static readonly string[] NotColor = { "normal", "nrm", "rough", "metal", "spec", "height", "disp", "_nor", "mask", "gloss", "_ao", "occlusion", "emiss" };
    private static readonly string[] Suffixes = { "instance", "material", "mat", "texture", "tex", "diffuse", "diff", "basecolor", "albedo", "colour", "color", "col", "png", "jpg", "jpeg", "001", "002" };

    private static bool Broken(Material m) =>
        m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !HasTexture(m);

    private static void RepairPortraitMaterials()
    {
        if (!AssetDatabase.IsValidFolder(PortraitRoot))
            return;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { PortraitRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                continue;
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!Broken(m))
                continue;
            string rest = path.Substring(PortraitRoot.Length + 1);
            string top = PortraitRoot + "/" + rest.Split('/')[0];
            RepairMaterial(m, m.name, top);
        }
        AssetDatabase.SaveAssets();
    }

    private static Material RepairMaterial(Material m, string name, string folder)
    {
        if (!Broken(m))
            return m;
        string key = Strip(Clean(name));
        if (key == "bark" || key == "foliage")
            return MilanoRoomBuilder.Mat(key == "bark" ? "Bark" : "Foliage", key == "bark" ? new Color(0.3f, 0.22f, 0.15f) : new Color(0.2f, 0.38f, 0.16f));

        Texture2D texture = MatchTexture(folder, key);
        if (texture == null)
            return m;

        Material target = m;
        string path = m != null ? AssetDatabase.GetAssetPath(m) : null;
        if (m == null || string.IsNullOrEmpty(path) || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
        {
            string fixedFolder = folder + "/_Fixed Materials";
            string fixedPath = fixedFolder + "/" + (key.Length > 0 ? key : Clean(texture.name)) + ".mat";
            target = AssetDatabase.LoadAssetAtPath<Material>(fixedPath);
            if (target == null)
            {
                EnsureFolder(fixedFolder);
                target = new Material(PortraitShader()) { name = name };
                AssetDatabase.CreateAsset(target, fixedPath);
            }
        }
        if (target.shader == null || target.shader.name == "Hidden/InternalErrorShader")
            target.shader = PortraitShader();
        if (target.HasProperty("_BaseMap"))
            target.SetTexture("_BaseMap", texture);
        if (target.HasProperty("_MainTex"))
            target.SetTexture("_MainTex", texture);
        if (target.HasProperty("_BaseColor") && target.GetColor("_BaseColor").maxColorComponent < 0.05f)
            target.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(target);
        return target;
    }

    private static Shader PortraitShader()
    {
        Shader retro = Shader.Find("Hortensia/Retro Lit");
        return retro != null ? retro : LitShader();
    }

    private static string Strip(string key)
    {
        bool again = true;
        while (again)
        {
            again = false;
            foreach (string suffix in Suffixes)
                if (key.Length > suffix.Length && key.EndsWith(suffix, StringComparison.Ordinal))
                {
                    key = key.Substring(0, key.Length - suffix.Length);
                    again = true;
                }
        }
        return key;
    }

    private static readonly Dictionary<string, string> TextureAliases = new Dictionary<string, string>
    {
        { "cardboardbox", "cardbaordbox" },
        { "wardrobe", "woodwardrobe" },
        { "clotherpiles", "clothespilestack" },
        { "clotheshanger", "hanger" },
        { "hangerbar", "hanger" },
        { "blanketontop", "fabric" },
        { "chest", "chest2" },
        { "bench", "benchtexture2" },
        { "candle2", "gothicfence" },
        { "sidetable1", "sidetable" },
        { "sidetable2", "sidetable" },
        { "drawer1", "drawer" },
        { "drawer2", "drawer" },
        { "officelamptransparent", "officelamp" },
    };

    private static Texture2D MatchTexture(string folder, string key)
    {
        if (TextureAliases.TryGetValue(key, out string alias))
            key = alias;
        if (!AssetDatabase.IsValidFolder(folder))
            return null;
        var candidates = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !NotColor.Any(n => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(n)))
            .Where(p => !p.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        if (candidates.Count == 0)
            return null;

        string best = null;
        int bestScore = 0;
        foreach (string p in candidates)
        {
            string tex = Strip(Clean(Path.GetFileNameWithoutExtension(p)));
            int score = key.Length == 0 ? 0
                : tex == key ? 4
                : tex.StartsWith(key, StringComparison.Ordinal) || key.StartsWith(tex, StringComparison.Ordinal) ? 3
                : tex.Contains(key) || key.Contains(tex) ? 2
                : 0;
            if (p.ToLowerInvariant().Contains("lowcolordepth"))
                score = Math.Max(0, score - 1);
            if (score > bestScore || (score == bestScore && score > 0 && best != null && p.Length < best.Length))
            {
                bestScore = score;
                best = p;
            }
        }
        if (best == null && candidates.Count == 1)
            best = candidates[0];
        return best != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(best) : null;
    }

    private static bool HasTexture(Material m)
    {
        if (m == null)
            return false;
        return (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) || (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null);
    }

    private static Shader LitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        return shader != null ? shader : Shader.Find("Standard");
    }

    private static void PrepareTextures()
    {
        var folders = new List<string>();
        if (AssetDatabase.IsValidFolder(ModelRoot))
            folders.Add(ModelRoot);
        if (AssetDatabase.IsValidFolder(TextureRoot))
            folders.Add(TextureRoot);
        if (folders.Count == 0)
            return;

        bool psx = Psx;
        int size = psx ? PsxSize : 1024;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", folders.ToArray()))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    continue;
                string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool normal = Normal.Any(k => file.Contains(k)) || file.Contains("_nor_");
                bool dirty = false;
                if (normal && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
                if (importer.maxTextureSize != size)
                {
                    importer.maxTextureSize = size;
                    dirty = true;
                }
                FilterMode filter = psx ? FilterMode.Point : FilterMode.Bilinear;
                if (importer.filterMode != filter)
                {
                    importer.filterMode = filter;
                    dirty = true;
                }
                if (importer.mipmapEnabled == psx)
                {
                    importer.mipmapEnabled = !psx;
                    dirty = true;
                }
                if (dirty)
                    importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    private static Material FindMaterial(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        string name = Path.GetFileNameWithoutExtension(path);
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            string found = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(found) == name && !found.StartsWith("Assets/_Trash"))
                return AssetDatabase.LoadAssetAtPath<Material>(found);
        }
        return null;
    }

    private static int ApplyTextures(Scene scene)
    {
        int count = 0;
        var renderers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToList();
        foreach (TextureEntry entry in Textures)
        {
            bool direct = entry.material.EndsWith(".mat", StringComparison.OrdinalIgnoreCase);
            Material material = direct ? FindMaterial(entry.material) : AssetDatabase.LoadAssetAtPath<Material>(GreyboxMaterials + "/" + entry.material + ".mat");
            if (material == null)
                continue;
            string folder = TextureFolder(entry.id);
            Texture2D diffuse = FindTexture(folder, entry.id, Diffuse);
            if (diffuse == null)
                continue;
            if (direct)
            {
                material.SetTexture("_BaseMap", diffuse);
                material.SetTextureScale("_BaseMap", Vector2.one * entry.tile);
                material.SetColor("_BaseColor", entry.tint);
                material.SetFloat("_Smoothness", 0.25f);
                Texture2D bump = FindTexture(folder, entry.id, Normal);
                if (bump != null)
                {
                    material.SetTexture("_BumpMap", bump);
                    material.SetTextureScale("_BumpMap", Vector2.one * entry.tile);
                    material.EnableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(material);
                count++;
                continue;
            }

            material.SetTexture("_BaseMap", diffuse);
            material.SetColor("_BaseColor", entry.tint);
            material.SetFloat("_Smoothness", 0.08f);
            Texture2D normal = FindTexture(folder, entry.id, Normal);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            count++;

            foreach (MeshRenderer r in renderers)
            {
                if (r.sharedMaterial != material || IsInsideModel(r.transform))
                    continue;
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                string mesh = filter.sharedMesh.name;
                Vector3 s = r.transform.lossyScale;
                s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                if (mesh == "Cube" || mesh.StartsWith("Box ", StringComparison.Ordinal))
                    filter.sharedMesh = TiledMesh(true, s, entry.tile);
                else if (mesh == "Quad" || mesh.StartsWith("Quad ", StringComparison.Ordinal))
                    filter.sharedMesh = TiledMesh(false, s, entry.tile);
            }
        }
        AssetDatabase.SaveAssets();
        return count;
    }

    private static Mesh TiledMesh(bool box, Vector3 size, float tile)
    {
        string name = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            box ? "Box {0:0.00}x{1:0.00}x{2:0.00} t{3:0.00}" : "Quad {0:0.00}x{1:0.00} t{3:0.00}", size.x, size.y, size.z, tile);
        string path = MeshFolder + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null)
            return mesh;

        mesh = new Mesh { name = name };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        if (box)
        {
            Face(Vector3.right, Vector3.back, Vector3.up, size.z, size.y, tile, vertices, uvs, normals, triangles);
            Face(Vector3.left, Vector3.forward, Vector3.up, size.z, size.y, tile, vertices, uvs, normals, triangles);
            Face(Vector3.forward, Vector3.right, Vector3.up, size.x, size.y, tile, vertices, uvs, normals, triangles);
            Face(Vector3.back, Vector3.left, Vector3.up, size.x, size.y, tile, vertices, uvs, normals, triangles);
            Face(Vector3.up, Vector3.right, Vector3.back, size.x, size.z, tile, vertices, uvs, normals, triangles);
            Face(Vector3.down, Vector3.right, Vector3.forward, size.x, size.z, tile, vertices, uvs, normals, triangles);
        }
        else
        {
            Face(Vector3.back, Vector3.left, Vector3.up, size.x, size.y, tile, vertices, uvs, normals, triangles, false);
        }
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EnsureFolder(MeshFolder);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void Face(Vector3 n, Vector3 u, Vector3 v, float width, float height, float tile,
        List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals, List<int> triangles, bool offset = true)
    {
        int start = vertices.Count;
        Vector3 c = offset ? n * 0.5f : Vector3.zero;
        Vector2[] corners = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };
        foreach (Vector2 k in corners)
        {
            vertices.Add(c + u * (0.5f * k.x) + v * (0.5f * k.y));
            uvs.Add(new Vector2((k.x * 0.5f + 0.5f) * width / tile, (k.y * 0.5f + 0.5f) * height / tile));
            normals.Add(n);
        }
        triangles.AddRange(new[] { start, start + 2, start + 3, start, start + 1, start + 2 });
    }
}
