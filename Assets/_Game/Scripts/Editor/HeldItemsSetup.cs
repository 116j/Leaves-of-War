using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HeldItemsSetup
{
    private const string LayerName = "HeldItems";

    [MenuItem("Tools/Leaves of War/Setup Held Items")]
    private static void Setup()
    {
        var log = new List<string>();
        if (!EnsureLayer(log))
            return;

        FirstPersonController player = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include)
            .FirstOrDefault(p => !EditorUtility.IsPersistent(p));
        Camera camera = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        if (camera == null)
        {
            EditorUtility.DisplayDialog("Setup Held Items", "No FirstPersonController with a camera in the open scene.", "OK");
            return;
        }

        var items = new List<Transform>();
        DiaryInteraction diary = Object.FindObjectsByType<DiaryInteraction>(FindObjectsInactive.Include).FirstOrDefault(d => !EditorUtility.IsPersistent(d));
        if (diary != null)
        {
            var so = new SerializedObject(diary);
            foreach (string field in new[] { "diary", "broom" })
                if (so.FindProperty(field).objectReferenceValue is Transform t)
                    items.Add(t);
        }
        if (items.Count == 0)
            items.AddRange(camera.transform.Cast<Transform>());

        HeldItemsCamera held = camera.GetComponent<HeldItemsCamera>();
        if (held == null)
            held = Undo.AddComponent<HeldItemsCamera>(camera.gameObject);
        var heldSo = new SerializedObject(held);
        SerializedProperty list = heldSo.FindProperty("items");
        list.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        heldSo.FindProperty("layerName").stringValue = LayerName;
        heldSo.ApplyModifiedProperties();
        log.Add("Held Items Camera on " + camera.name + ": " + string.Join(", ", items.Select(t => t.name)) + ".");

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Setup Held Items\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Setup Held Items", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static bool EnsureLayer(List<string> log)
    {
        if (LayerMask.NameToLayer(LayerName) >= 0)
            return true;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
                continue;
            layer.stringValue = LayerName;
            tagManager.ApplyModifiedProperties();
            log.Add("Layer '" + LayerName + "' created (" + i + ").");
            return true;
        }

        EditorUtility.DisplayDialog("Setup Held Items", "No free layer for " + LayerName + ".", "OK");
        return false;
    }
}
