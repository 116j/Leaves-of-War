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
            log.Add("Player settings copied from " + Path.GetFileNameWithoutExtension(source) + ": " + (copied.Count > 0 ? string.Join(", ", copied) : "nothing to copy") + ".");
        }
        finally
        {
            EditorSceneManager.CloseScene(from, true);
            SceneManager.SetActiveScene(target);
        }
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
