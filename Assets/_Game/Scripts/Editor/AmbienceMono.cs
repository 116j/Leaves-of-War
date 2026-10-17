using System.IO;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AmbienceMono
{
    public const string Folder = "Assets/_Game/Audio/Ambience/Mono";

    [MenuItem("Tools/Leaves of War/Audio/Mono Ambience Copies (open scene)")]
    private static void Menu()
    {
        Scene scene = SceneManager.GetActiveScene();
        int count = 0;
        foreach (LevelAudio audio in Object.FindObjectsByType<LevelAudio>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(audio);
            SerializedProperty list = so.FindProperty("ambience");
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                var clip = entry.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
                if (clip == null || entry.FindPropertyRelative("source").objectReferenceValue != null)
                    continue;
                AudioClip mono = Copy(clip);
                if (mono == null)
                    continue;
                entry.FindPropertyRelative("monoClip").objectReferenceValue = mono;
                count++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (count > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Mono Ambience", count > 0
            ? count + " ambience loop(s) now have a mono copy in " + Folder + ".\n\nSave the scene (Ctrl+S)."
            : "No ambience in the Level Audio of the open scene.", "OK");
    }

    public static AudioClip Copy(AudioClip clip)
    {
        if (clip == null)
            return null;
        string source = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(source) || source.StartsWith(Folder))
            return null;
        EnsureFolder(Folder);
        string target = Folder + "/" + Path.GetFileNameWithoutExtension(source) + " Mono" + Path.GetExtension(source);
        bool stale = !File.Exists(target) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(target);
        if (stale)
        {
            if (File.Exists(target))
                AssetDatabase.DeleteAsset(target);
            if (!AssetDatabase.CopyAsset(source, target))
                return null;
        }
        if (AssetImporter.GetAtPath(source) is AudioImporter original && original.defaultSampleSettings.loadType == AudioClipLoadType.Streaming)
        {
            AudioImporterSampleSettings settings = original.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            original.defaultSampleSettings = settings;
            original.SaveAndReimport();
        }
        if (AssetImporter.GetAtPath(source) is AudioImporter from && AssetImporter.GetAtPath(target) is AudioImporter to)
        {
            to.defaultSampleSettings = from.defaultSampleSettings;
            to.loadInBackground = from.loadInBackground;
            to.forceToMono = true;
            var so = new SerializedObject(to);
            SerializedProperty normalize = so.FindProperty("m_Normalize");
            if (normalize != null)
            {
                normalize.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            to.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(target);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
