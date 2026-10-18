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

public static class CarryLevelSettings
{
    private const string Scenes = "Assets/_Game/Scenes";

    private static readonly Type[] Carried =
    {
        typeof(FirstPersonController), typeof(DiaryInteraction), typeof(TranscribePoem), typeof(BroomSweeper), typeof(HeldItemsCamera),
        typeof(HeadBob), typeof(FollowerLeaf), typeof(DialogueRunner), typeof(InteractionPromptHud), typeof(ObjectInspector),
        typeof(Footsteps), typeof(ReadingBlur)
    };

    private static readonly HashSet<string> Skip = new HashSet<string> { "m_Script", "m_Enabled", "_earlierPoems", "_firstPoemId", "startInHand" };

    [MenuItem("Tools/Leaves of War/Copy player settings from the previous level")]
    private static void Menu()
    {
        Scene scene = SceneManager.GetActiveScene();
        var log = new List<string>();
        FromPreviousLevel(scene, log);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Player Settings", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    public static void FromPreviousLevel(Scene target, List<string> log)
    {
        int number = Number(target.name);
        string source = null;
        for (int n = number - 1; n >= 1 && source == null; n--)
            source = Directory.Exists(Scenes) ? Directory.GetFiles(Scenes, "Level " + n + " - *.unity").FirstOrDefault() : null;
        if (number <= 1 || source == null)
        {
            log.Add("Player settings: no earlier level to copy from.");
            return;
        }
        source = source.Replace('\\', '/');
        Scene from = EditorSceneManager.OpenScene(source, OpenSceneMode.Additive);
        try
        {
            var copied = new List<string>();
            foreach (Type type in Carried)
            {
                Component a = Find(from, type), b = Find(target, type);
                if (a == null || b == null)
                    continue;
                int count = CopyValues(a, b);
                if (count > 0)
                    copied.Add(type.Name);
            }
            DiaryInteraction fromDiary = Find(from, typeof(DiaryInteraction)) as DiaryInteraction;
            DiaryInteraction toDiary = Find(target, typeof(DiaryInteraction)) as DiaryInteraction;
            if (fromDiary != null && toDiary != null)
                foreach (string field in new[] { "diary", "broom" })
                {
                    Transform a = new SerializedObject(fromDiary).FindProperty(field)?.objectReferenceValue as Transform;
                    Transform b = new SerializedObject(toDiary).FindProperty(field)?.objectReferenceValue as Transform;
                    if (a == null || b == null)
                        continue;
                    Undo.RecordObject(b, "Carry held item");
                    b.localPosition = a.localPosition;
                    b.localRotation = a.localRotation;
                    b.localScale = a.localScale;
                    copied.Add(field + " in hand");
                }
            if (fromDiary != null && toDiary != null)
            {
                Transform a = new SerializedObject(fromDiary).FindProperty("diary")?.objectReferenceValue as Transform;
                Transform b = new SerializedObject(toDiary).FindProperty("diary")?.objectReferenceValue as Transform;
                if (a != null && b != null)
                    copied.Add(CopyNotebook(a, b));
            }
            log.Add("Player settings copied from " + Path.GetFileNameWithoutExtension(source) + ": " + (copied.Count > 0 ? string.Join(", ", copied) : "nothing to copy") + ".");
        }
        finally
        {
            EditorSceneManager.CloseScene(from, true);
            SceneManager.SetActiveScene(target);
        }
    }

    [MenuItem("Tools/Leaves of War/Copy notebook layout from the previous level")]
    private static void NotebookMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        var log = new List<string>();
        FromPreviousLevel(scene, log);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Notebook", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static readonly HashSet<string> NotebookSkip = new HashSet<string> { "m_Script", "m_text", "m_GameObject", "m_Father", "m_Children", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" };

    private static string CopyNotebook(Transform from, Transform to)
    {
        var targets = new Dictionary<string, Transform>();
        foreach (Transform t in to.GetComponentsInChildren<Transform>(true))
            targets[HierarchyPath(to, t)] = t;
        int matched = 0, missing = 0, components = 0;
        foreach (Transform source in from.GetComponentsInChildren<Transform>(true))
        {
            if (source == from)
                continue;
            if (!targets.TryGetValue(HierarchyPath(from, source), out Transform target))
            {
                missing++;
                continue;
            }
            matched++;
            if (source.gameObject.activeSelf != target.gameObject.activeSelf)
                target.gameObject.SetActive(source.gameObject.activeSelf);
            var used = new Dictionary<Type, int>();
            foreach (Component c in source.GetComponents<Component>())
            {
                if (c == null)
                    continue;
                Type type = c.GetType();
                used.TryGetValue(type, out int n);
                used[type] = n + 1;
                Component[] same = target.GetComponents(type);
                if (n >= same.Length)
                    continue;
                if (CopyAll(c, same[n]) > 0)
                    components++;
            }
        }
        return "notebook layout (" + matched + " parts, " + components + " components" + (missing > 0 ? ", " + missing + " parts not in this notebook" : "") + ")";
    }

    private static string HierarchyPath(Transform root, Transform t)
    {
        var parts = new List<string>();
        for (Transform p = t; p != null && p != root; p = p.parent)
        {
            int index = 0;
            if (p.parent != null)
                for (int i = 0; i < p.GetSiblingIndex(); i++)
                    if (p.parent.GetChild(i).name == p.name)
                        index++;
            parts.Add(p.name + "#" + index);
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static int CopyAll(Component from, Component to)
    {
        var a = new SerializedObject(from);
        var b = new SerializedObject(to);
        int count = 0;
        SerializedProperty p = a.GetIterator();
        bool enter = true;
        while (p.Next(enter))
        {
            enter = false;
            string root = p.propertyPath.Split('.')[0];
            if (NotebookSkip.Contains(root) || p.propertyPath.Contains(".Array.") || p.isArray && p.propertyType != SerializedPropertyType.String)
                continue;
            if (p.propertyType == SerializedPropertyType.Generic)
            {
                enter = true;
                continue;
            }
            if (p.propertyType == SerializedPropertyType.ExposedReference || p.propertyType == SerializedPropertyType.ManagedReference)
                continue;
            if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null && !EditorUtility.IsPersistent(p.objectReferenceValue))
                continue;
            SerializedProperty q = b.FindProperty(p.propertyPath);
            if (q == null || q.propertyType != p.propertyType)
                continue;
            b.CopyFromSerializedProperty(p);
            count++;
        }
        b.ApplyModifiedPropertiesWithoutUndo();
        return count;
    }

    private static int Number(string name)
    {
        string digits = new string(name.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out int n) ? n : 0;
    }

    private static Component Find(Scene scene, Type type) =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren(type, true)).FirstOrDefault();

    private static int CopyValues(Component from, Component to)
    {
        var a = new SerializedObject(from);
        var b = new SerializedObject(to);
        int count = 0;
        SerializedProperty p = a.GetIterator();
        bool enter = true;
        while (p.Next(enter))
        {
            enter = false;
            string root = p.propertyPath.Split('.')[0];
            if (Skip.Contains(root) || p.isArray && p.propertyType != SerializedPropertyType.String)
                continue;
            if (p.propertyType == SerializedPropertyType.Generic)
            {
                enter = true;
                continue;
            }
            if (p.propertyType == SerializedPropertyType.ObjectReference || p.propertyType == SerializedPropertyType.ExposedReference ||
                p.propertyType == SerializedPropertyType.ManagedReference || p.propertyPath.Contains(".Array."))
                continue;
            SerializedProperty q = b.FindProperty(p.propertyPath);
            if (q == null || q.propertyType != p.propertyType)
                continue;
            b.CopyFromSerializedProperty(p);
            count++;
        }
        b.ApplyModifiedPropertiesWithoutUndo();
        return count;
    }
}
