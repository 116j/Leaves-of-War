using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class ProjectTidy : EditorWindow
{
    private const string Trash = "Assets/_Trash";
    private const string Separator = "────";

    private enum Kind { Move, MoveOrTrash, Trash }

    private sealed class Rule
    {
        public readonly string from;
        public readonly string to;
        public readonly Kind kind;

        public Rule(string from, string to, Kind kind = Kind.Move)
        {
            this.from = from;
            this.to = to;
            this.kind = kind;
        }
    }

    private sealed class Step
    {
        public string from;
        public string to;
        public string note;
        public bool on = true;
    }

    private static readonly Rule[] Rules =
    {
        new Rule("Assets/_Game/Scripts/Player/AquilineTwo.ttf", "Assets/_Game/Art/Fonts"),
        new Rule("Assets/_Game/Scripts/Player/AquilineTwo SDF (Bold).asset", "Assets/_Game/Art/Fonts"),
        new Rule("Assets/_Game/Scripts/Player/AquilineTwo SDF(Light Bold).asset", "Assets/_Game/Art/Fonts/AquilineTwo SDF (Light Bold).asset"),
        new Rule("Assets/_Game/Scripts/Player/Diary_Cover.mat", "Assets/_Game/Materials/Props"),
        new Rule("Assets/_Game/Scripts/Player/TransporantMaterial.mat", "Assets/_Game/Materials/Props/Transparent.mat"),
        new Rule("Assets/_Game/Art/Materials/broom_hair.mat", "Assets/_Game/Materials/Props/Broom Hair.mat"),
        new Rule("Assets/_Game/Scripts/Player/Old_Paper.png", "Assets/_Game/Art/2D/Paper/Old Paper.png"),
        new Rule("Assets/_Game/Scripts/Player/HeadBob.asset", "Assets/_Game/Data"),
        new Rule("Assets/_Game/Scripts/Player/PoemInfo.asset", "Assets/_Game/Data"),
        new Rule("Assets/_Game/Audio/Test/Carlo.controller", "Assets/_Game/Art/Characters/Carlo"),
        new Rule("Assets/_Game/Audio/Test/Carlo_Idle.anim", "Assets/_Game/Art/Characters/Carlo"),
        new Rule("Assets/_Game/Audio/Test/Carlo_Talking.anim", "Assets/_Game/Art/Characters/Carlo"),
        new Rule("Assets/_Game/Audio/Test/Carlo_waiting.anim", "Assets/_Game/Art/Characters/Carlo/Carlo_Waiting.anim"),
        new Rule("Assets/_Game/Audio/Test/*.wav", "Assets/_Game/Audio/Voices/Placeholder", Kind.MoveOrTrash),
        new Rule("Assets/Resources/Audio/test_*.wav", "Assets/_Game/Audio/Voices/Placeholder", Kind.MoveOrTrash),
        new Rule("Assets/Tree.prefab", "Assets/_Game/Art/3D/Trees", Kind.MoveOrTrash),
        new Rule("Assets/Tree_Textures", "Assets/_Game/Art/3D/Trees/Textures", Kind.MoveOrTrash),
        new Rule("Assets/Art/Characters", "Assets/_Game/Art/Characters/Willowet", Kind.MoveOrTrash),
        new Rule("Assets/Art/PS1_World.renderTexture", "Assets/_Game/Art/RenderTextures", Kind.MoveOrTrash),
        new Rule("Assets/_Game/Scenes/Old_Paper.png", "Assets/_Game/Art/2D/Paper/Old Paper (2).png", Kind.MoveOrTrash),
        new Rule("Assets/_Game/Scenes/Core.unity", null, Kind.Trash),
        new Rule("Assets/_Game/Scenes/Core 1.unity", null, Kind.Trash),
        new Rule("Assets/_Game/Scripts/Dialogue/Test", null, Kind.MoveOrTrash),
        new Rule("Assets/Book-Page Curl/Example", null, Kind.MoveOrTrash),
        new Rule("Assets/Bitwave_Labs/AnimatedTextReveal/Demos", null, Kind.MoveOrTrash),
        new Rule("Assets/ChocDino/UIFX/Demos", null, Kind.MoveOrTrash),
        new Rule("Assets/*.prefab", "Assets/_Game/Prefabs", Kind.MoveOrTrash),
        new Rule("Assets/*.mat", "Assets/_Game/Materials", Kind.MoveOrTrash),
        new Rule("Assets/*.png", "Assets/_Game/Art/2D", Kind.MoveOrTrash),
        new Rule("Assets/*.jpg", "Assets/_Game/Art/2D", Kind.MoveOrTrash),
        new Rule("Assets/*.fbx", "Assets/_Game/Art/3D", Kind.MoveOrTrash),
        new Rule("Assets/*.glb", "Assets/_Game/Art/3D", Kind.MoveOrTrash),
        new Rule("Assets/*.unity", null, Kind.Trash),
    };

    private static readonly string[] EmptyFolders =
    {
        "Assets/Art", "Assets/_Game/Art/Materials", "Assets/_Game/Audio/Test", "Assets/Tree_Textures",
        "Assets/Resources/Audio", "Assets/Book-Page Curl", "Assets/Bitwave_Labs"
    };

    private static readonly string[] TestObjects = { "Cube", "Sphere", "Cylinder", "Plane", "Capsule", "Quad", "Test Tools" };

    private List<Step> steps = new List<Step>();
    private Vector2 scroll;
    private bool planned;
    private bool allLevels = true;
    private string sceneReport = "";

    [MenuItem("Tools/Leaves of War/Project Tidy")]
    private static void Open()
    {
        GetWindow<ProjectTidy>("Project Tidy").minSize = new Vector2(620f, 420f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("1. Scenes", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Removes test objects (Cube, Sphere, Cylinder, Plane, Test Tools) and inactive placeholder notes (Note2+), " +
            "only when nothing else in the scene points at them. Then orders the root objects under separators. Nothing is reparented.", MessageType.None);
        allLevels = EditorGUILayout.ToggleLeft("All \"Level\" scenes in Build Settings (otherwise only the open scene)", allLevels);
        if (GUILayout.Button("Clean scenes", GUILayout.Height(26f)))
            CleanScenes();
        if (!string.IsNullOrEmpty(sceneReport))
            EditorGUILayout.HelpBox(sceneReport, MessageType.Info);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("2. Assets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Moves keep every reference (GUIDs stay the same). Nothing is deleted: unused files go to " + Trash +
            " with their original path, and \"Restore\" puts them back. Empty the trash only after testing the game.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan", GUILayout.Height(26f)))
                Plan();
            using (new EditorGUI.DisabledScope(!planned || steps.All(s => !s.on)))
                if (GUILayout.Button("Apply selected", GUILayout.Height(26f)))
                    Apply();
        }

        if (planned)
        {
            if (steps.Count == 0)
                EditorGUILayout.HelpBox("Nothing to tidy.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (Step step in steps)
                using (new EditorGUILayout.HorizontalScope())
                {
                    step.on = EditorGUILayout.Toggle(step.on, GUILayout.Width(18f));
                    GUIStyle style = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
                    string target = step.to.StartsWith(Trash) ? "<color=#d08060>TRASH</color>" : step.to;
                    EditorGUILayout.LabelField(step.from + "  →  " + target + (string.IsNullOrEmpty(step.note) ? "" : "   <i>(" + step.note + ")</i>"), style);
                }
            EditorGUILayout.EndScrollView();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Trash", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            bool hasTrash = AssetDatabase.IsValidFolder(Trash);
            using (new EditorGUI.DisabledScope(!hasTrash))
            {
                if (GUILayout.Button("Show"))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(Trash));
                if (GUILayout.Button("Restore all"))
                    RestoreTrash();
                if (GUILayout.Button("Empty trash (delete)"))
                    EmptyTrash();
            }
        }
    }

    private void CleanScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        string active = SceneManager.GetActiveScene().path;
        var paths = (allLevels ? EditorBuildSettings.scenes.Select(s => s.path).ToList() : new List<string> { active })
            .Where(p => File.Exists(p) && Path.GetFileName(p).StartsWith("Level")).ToList();
        if (paths.Count == 0)
        {
            sceneReport = "No level scene to clean (only scenes named \"Level ...\" are touched).";
            return;
        }
        var report = new List<string>();
        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var removed = new List<string>();
            var kept = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!IsTestObject(root))
                    continue;
                string referrer = Referrer(scene, root);
                if (referrer != null)
                {
                    kept.Add(root.name + " (used by " + referrer + ")");
                    continue;
                }
                removed.Add(root.name);
                Object.DestroyImmediate(root);
            }
            int ordered = Organize(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add(Path.GetFileNameWithoutExtension(path) + ": removed " + (removed.Count == 0 ? "nothing" : string.Join(", ", removed)) +
                (kept.Count > 0 ? "; kept " + string.Join(", ", kept) : "") + "; " + ordered + " root objects ordered.");
        }
        if (!string.IsNullOrEmpty(active) && File.Exists(active))
            EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        sceneReport = string.Join("\n", report);
        planned = false;
    }

    private static bool IsTestObject(GameObject root)
    {
        string name = root.name.Trim();
        if (TestObjects.Contains(name))
            return true;
        if (name.StartsWith("Note") && int.TryParse(name.Substring(4), out int n) && n >= 2 && !root.activeSelf)
            return root.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name == "AssignPoem");
        return false;
    }

    private static string Referrer(Scene scene, GameObject candidate)
    {
        var inside = new HashSet<Object>();
        foreach (Transform t in candidate.GetComponentsInChildren<Transform>(true))
        {
            inside.Add(t.gameObject);
            foreach (Component c in t.GetComponents<Component>())
                if (c != null)
                    inside.Add(c);
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == candidate)
                continue;
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform)
                    continue;
                SerializedProperty p = new SerializedObject(c).GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null && inside.Contains(p.objectReferenceValue))
                        return c.gameObject.name + "/" + c.GetType().Name;
            }
        }
        return null;
    }

    private static readonly string[] Groups = { "SYSTEMS", "PLAYER & UI", "LEVEL", "CHARACTERS & INTERACTABLES", "EFFECTS" };

    private static int Organize(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name.StartsWith(Separator))
                Object.DestroyImmediate(root);

        var roots = scene.GetRootGameObjects();
        var buckets = Groups.Select(_ => new List<GameObject>()).ToArray();
        foreach (GameObject root in roots)
            buckets[Group(root)].Add(root);

        int index = 0;
        for (int g = 0; g < Groups.Length; g++)
        {
            if (buckets[g].Count == 0)
                continue;
            var header = new GameObject(Separator + " " + Groups[g] + " " + Separator) { tag = "EditorOnly" };
            SceneManager.MoveGameObjectToScene(header, scene);
            header.transform.SetSiblingIndex(index++);
            foreach (GameObject root in buckets[g])
                root.transform.SetSiblingIndex(index++);
        }
        return roots.Length;
    }

    private static int Group(GameObject root)
    {
        var names = new HashSet<string>(root.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name));
        bool Has(params string[] types) => types.Any(names.Contains);

        if (Has("FirstPersonController", "Canvas"))
            return 1;
        if (Has("LeafField", "FollowerLeaf", "ParticleSystem"))
            return 4;
        if (Has("LevelFlow", "DialogueRunner", "EventSystem", "Volume", "TranscribePoem", "FadeScreen", "GazeManager", "AudioListener") ||
            root.name.Contains("Manager") || root.name.Contains("Flow"))
            return 0;
        Light light = root.GetComponent<Light>();
        if (light != null && root.transform.childCount == 0)
            return 0;
        if (Has("AssignPoem", "DialogueTrigger", "Animator", "Examinable") ||
            root.GetComponents<Component>().Any(c => c != null && c.GetType().GetInterfaces().Any(i => i.Name == "IInteractable")))
            return 3;
        return 2;
    }

    private void Plan()
    {
        steps.Clear();
        HashSet<string> used = UsedAssets();
        var claimed = new HashSet<string>();
        foreach (Rule rule in Rules)
            foreach (string from in Expand(rule.from))
            {
                if (!claimed.Add(from))
                    continue;
                bool inUse = IsUsed(from, used);
                string to;
                string note;
                if (rule.kind == Kind.Trash || (rule.kind == Kind.MoveOrTrash && !inUse))
                {
                    to = Trash + from.Substring("Assets".Length);
                    note = inUse ? "in use!" : "not used by any build scene or Resources";
                }
                else if (rule.to == null)
                    continue;
                else
                {
                    to = Target(from, rule.to);
                    note = rule.kind == Kind.MoveOrTrash ? "in use" : "";
                }
                if (to == from)
                    continue;
                steps.Add(new Step { from = from, to = to, note = note, on = !(to.StartsWith(Trash) && inUse) });
            }
        planned = true;
    }

    private static string Target(string from, string to)
    {
        string ext = Path.GetExtension(to);
        if (!string.IsNullOrEmpty(ext) && !AssetDatabase.IsValidFolder(from))
            return to;
        if (AssetDatabase.IsValidFolder(from) && !AssetDatabase.IsValidFolder(to))
            return to;
        return to + "/" + Path.GetFileName(from);
    }

    private static IEnumerable<string> Expand(string pattern)
    {
        if (!pattern.Contains("*"))
        {
            if (AssetDatabase.IsValidFolder(pattern) || File.Exists(pattern))
                yield return pattern;
            yield break;
        }
        string folder = Path.GetDirectoryName(pattern).Replace('\\', '/');
        string mask = Path.GetFileName(pattern);
        if (!Directory.Exists(folder))
            yield break;
        foreach (string file in Directory.GetFiles(folder, mask, SearchOption.TopDirectoryOnly).OrderBy(f => f))
            if (!file.EndsWith(".meta"))
                yield return file.Replace('\\', '/');
    }

    private static HashSet<string> UsedAssets()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        var resources = AssetDatabase.GetAllAssetPaths().Where(p => p.Contains("/Resources/") && !p.StartsWith(Trash) && !AssetDatabase.IsValidFolder(p)).ToList();
        var used = new HashSet<string>(AssetDatabase.GetDependencies(scenes.ToArray(), true));
        foreach (string resource in resources)
            foreach (string dependency in AssetDatabase.GetDependencies(resource, true))
                if (dependency != resource)
                    used.Add(dependency);
        return used;
    }

    private static bool IsUsed(string path, HashSet<string> used)
    {
        if (!AssetDatabase.IsValidFolder(path))
            return used.Contains(path);
        string prefix = path + "/";
        return used.Any(u => u.StartsWith(prefix));
    }

    private void Apply()
    {
        var chosen = steps.Where(s => s.on).ToList();
        if (!EditorUtility.DisplayDialog("Project Tidy", chosen.Count + " moves (" + chosen.Count(s => s.to.StartsWith(Trash)) + " to the trash). Continue?", "Apply", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var errors = new List<string>();
        foreach (Step step in chosen)
        {
            EnsureFolder(Path.GetDirectoryName(step.to).Replace('\\', '/'));
            string to = Exists(step.to) ? AssetDatabase.GenerateUniqueAssetPath(step.to) : step.to;
            string error = AssetDatabase.MoveAsset(step.from, to);
            if (!string.IsNullOrEmpty(error))
                errors.Add(step.from + ": " + error);
        }
        foreach (string folder in EmptyFolders)
            DeleteIfEmpty(folder);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Plan();
        EditorUtility.DisplayDialog("Project Tidy", errors.Count == 0 ? "Done." : string.Join("\n", errors), "OK");
    }

    private void RestoreTrash()
    {
        var items = new List<string>();
        Collect(Trash, items);
        var errors = new List<string>();
        foreach (string item in items)
        {
            string back = "Assets" + item.Substring(Trash.Length);
            if (Exists(back))
            {
                errors.Add(back + " already exists");
                continue;
            }
            EnsureFolder(Path.GetDirectoryName(back).Replace('\\', '/'));
            string error = AssetDatabase.MoveAsset(item, back);
            if (!string.IsNullOrEmpty(error))
                errors.Add(item + ": " + error);
        }
        DeleteIfEmpty(Trash);
        AssetDatabase.Refresh();
        planned = false;
        EditorUtility.DisplayDialog("Project Tidy", errors.Count == 0 ? "Restored " + items.Count + " items." : string.Join("\n", errors), "OK");
    }

    private static void Collect(string folder, List<string> items)
    {
        foreach (string sub in AssetDatabase.GetSubFolders(folder))
        {
            string original = "Assets" + sub.Substring(Trash.Length);
            if (AssetDatabase.IsValidFolder(original))
                Collect(sub, items);
            else
                items.Add(sub);
        }
        foreach (string file in Directory.GetFiles(folder).Where(f => !f.EndsWith(".meta")))
            items.Add(file.Replace('\\', '/'));
    }

    private void EmptyTrash()
    {
        if (!EditorUtility.DisplayDialog("Project Tidy", "Permanently delete everything in " + Trash + "?\nDo this only after playing the whole game once.", "Delete", "Cancel"))
            return;
        AssetDatabase.DeleteAsset(Trash);
        AssetDatabase.Refresh();
        planned = false;
    }

    private static bool Exists(string path)
    {
        return AssetDatabase.IsValidFolder(path) || File.Exists(path);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void DeleteIfEmpty(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return;
        foreach (string sub in AssetDatabase.GetSubFolders(folder))
            DeleteIfEmpty(sub);
        if (AssetDatabase.GetSubFolders(folder).Length == 0 && !Directory.GetFiles(folder).Any(f => !f.EndsWith(".meta")))
            AssetDatabase.DeleteAsset(folder);
    }
}
