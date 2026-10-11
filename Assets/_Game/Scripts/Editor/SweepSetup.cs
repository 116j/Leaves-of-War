using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SweepSetup
{
    private const string MaterialPath = "Assets/_Game/Materials/Leaf.mat";
    private const string TexturePath = "Assets/Resources/MainMenu/GoldenLeaf.png";

    [MenuItem("Tools/Leaves of War/Setup Sweeping")]
    private static void Setup()
    {
        var log = new List<string>();

        FirstPersonController player = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include)
            .FirstOrDefault(p => !EditorUtility.IsPersistent(p));
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Sweeping", "No FirstPersonController in the open scene.", "OK");
            return;
        }

        BroomSweeper sweeper = player.GetComponent<BroomSweeper>();
        if (sweeper == null)
        {
            sweeper = Undo.AddComponent<BroomSweeper>(player.gameObject);
            log.Add("Broom Sweeper added to " + player.name + ".");
        }

        Transform broom = FindBroom(player);
        var so = new SerializedObject(sweeper);
        so.FindProperty("broom").objectReferenceValue = broom;
        so.ApplyModifiedProperties();
        log.Add(broom != null ? "Broom: " + broom.name + "." : "WARNING: broom not found, assign it on Broom Sweeper.");

        LeafField field = Object.FindObjectsByType<LeafField>(FindObjectsInactive.Include).FirstOrDefault(f => !EditorUtility.IsPersistent(f));
        if (field == null)
        {
            var go = new GameObject("Leaf Field");
            Undo.RegisterCreatedObjectUndo(go, "Setup Sweeping");
            field = go.AddComponent<LeafField>();
            log.Add("Leaf Field created.");
        }

        Material material = LeafMaterial(log);
        var fieldSo = new SerializedObject(field);
        fieldSo.FindProperty("leafMaterial").objectReferenceValue = material;
        fieldSo.FindProperty("leafTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        fieldSo.ApplyModifiedProperties();

        int poems = Object.FindObjectsByType<AssignPoem>(FindObjectsInactive.Include).Count(p => !EditorUtility.IsPersistent(p));
        log.Add(poems + " poems on the floor will be covered by leaves.");

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Setup Sweeping\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Setup Sweeping", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static Transform FindBroom(FirstPersonController player)
    {
        DiaryInteraction diary = Object.FindObjectsByType<DiaryInteraction>(FindObjectsInactive.Include).FirstOrDefault(d => !EditorUtility.IsPersistent(d));
        if (diary != null)
        {
            Object assigned = new SerializedObject(diary).FindProperty("broom").objectReferenceValue;
            if (assigned is Transform t)
                return t;
        }
        return player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLowerInvariant().Contains("broom"));
    }

    private static Material LeafMaterial(List<string> log)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            log.Add("WARNING: URP Lit shader not found, leaves use a runtime material.");
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        material = new Material(shader) { name = "Leaf", enableInstancing = true };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.4f);
        material.SetFloat("_Cull", 0f);
        material.SetFloat("_Smoothness", 0.1f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.doubleSidedGI = true;
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        log.Add("Leaf material created: " + MaterialPath + ".");
        return material;
    }
}
