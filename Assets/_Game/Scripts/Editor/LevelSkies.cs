using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelSkies
{
    private const string Folder = "Assets/_Game/Art/Skyboxes/Materials";
    private const string ObjectName = "Level Sky";

    private sealed class Sky
    {
        public string scene, material, later;
        public Color tint = Neutral, laterTint = Neutral;
        public float exposure = 1f, laterExposure = 1f;
        public int poem;
    }

    private static readonly Color Neutral = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private static readonly Sky[] Skies =
    {
        new Sky { scene = "Milano", material = "SkyBrightMorning", tint = new Color(0.47f, 0.48f, 0.5f, 0.5f), exposure = 0.85f },
        new Sky { scene = "Parigi", material = "SkyAfterNoon", exposure = 0.79f },
        new Sky { scene = "Carso", material = "SkyNight", exposure = 0.55f, later = "SkyBrightMorning", laterTint = new Color(0.42f, 0.42f, 0.47f, 0.5f), laterExposure = 0.6f, poem = 3 },
        new Sky { scene = "San Paolo", material = "SkyNoon", tint = new Color(0.36f, 0.38f, 0.4f, 0.5f), exposure = 0.55f },
        new Sky { scene = "Roma", material = "SkySunset", exposure = 0.8f },
        new Sky { scene = "Courton", material = "SkyNight", exposure = 0.7f },
    };

    [MenuItem("Tools/Leaves of War/Skies (all levels)")]
    private static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        string active = SceneManager.GetActiveScene().path;
        var log = new List<string>();
        foreach (string path in Directory.GetFiles("Assets/_Game/Scenes", "Level *.unity").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            Sky sky = Skies.FirstOrDefault(s => name.Contains(s.scene));
            if (sky == null)
            {
                log.Add(name + ": no sky chosen for this level (add it to LevelSkies).");
                continue;
            }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            log.Add(name + ": " + Apply(scene, sky));
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(active) && File.Exists(active))
            EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        string text = string.Join("\n", log);
        Debug.Log("Skies\n" + text);
        EditorUtility.DisplayDialog("Skies", text, "OK");
    }

    private static string Apply(Scene scene, Sky sky)
    {
        Material material = Load(sky.material);
        if (material == null)
            return "skybox " + sky.material + " missing in " + Folder + ".";
        LevelSky component = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelSky>(true)).FirstOrDefault();
        if (component == null)
            component = new GameObject(ObjectName).AddComponent<LevelSky>();
        var so = new SerializedObject(component);
        so.FindProperty("sky").objectReferenceValue = material;
        so.FindProperty("tint").colorValue = sky.tint;
        so.FindProperty("exposure").floatValue = sky.exposure;
        Material later = string.IsNullOrEmpty(sky.later) ? null : Load(sky.later);
        so.FindProperty("laterSky").objectReferenceValue = later;
        so.FindProperty("laterTint").colorValue = sky.laterTint;
        so.FindProperty("laterExposure").floatValue = sky.laterExposure;
        so.FindProperty("poemId").intValue = sky.poem;
        DawnAfterPoem dawn = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DawnAfterPoem>(true)).FirstOrDefault();
        if (dawn != null)
        {
            var dawnSo = new SerializedObject(dawn);
            so.FindProperty("delay").floatValue = dawnSo.FindProperty("delay").floatValue;
            so.FindProperty("seconds").floatValue = dawnSo.FindProperty("seconds").floatValue;
            so.FindProperty("poemId").intValue = dawnSo.FindProperty("poemId").intValue;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        RenderSettings.skybox = material;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                if (camera.CompareTag("MainCamera"))
                    camera.clearFlags = CameraClearFlags.Skybox;
        EditorSceneManager.MarkSceneDirty(scene);
        return sky.material + (later != null ? ", turning to " + sky.later + " after the poem" : "");
    }

    private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
}
