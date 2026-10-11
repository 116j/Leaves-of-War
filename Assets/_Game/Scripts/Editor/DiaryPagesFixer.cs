using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DiaryPagesFixer
{
    private static readonly Vector4 BodyMargin = new Vector4(0.03f, 0.02f, 0.03f, 0.02f);
    private static readonly Vector4 HeaderMargin = new Vector4(0.03f, 0f, 0.03f, 0f);

    private static readonly (string first, string second, string field)[] Spreads =
    {
        ("[Current]Poem_Page_0_Text (TMP)", "[Current]Poem_Name_Page_1_Text (TMP)", "_assignedPoemTextCurrentNext"),
        ("[Old]Poem_Page_1_Text (TMP)", "[Old]Poem_Name_Page_0_Text (TMP)", "_assignedPoemTextOldNext")
    };

    private static readonly string[] Headers =
    {
        "[Current]Poem_Date_Text (TMP)", "[Current]Poem_Name_Text (TMP)", "[Current]Poem_Place_Text (TMP)",
        "[Old]Poem_Date_Text (TMP)", "[Old]Poem_Name_Text (TMP)", "[Old]Poem_Place_Text (TMP)"
    };

    [MenuItem("Tools/Leaves of War/Fix Diary Pages")]
    private static void Fix()
    {
        var texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include)
            .Where(t => !EditorUtility.IsPersistent(t))
            .GroupBy(t => t.name.Trim())
            .ToDictionary(g => g.Key, g => g.First());
        var log = new List<string>();
        MonoBehaviour transcribe = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .FirstOrDefault(m => m != null && m.GetType().Name == "TranscribePoem");
        SerializedObject transcribeSo = transcribe != null ? new SerializedObject(transcribe) : null;

        CenterPages(log);

        foreach (var (firstName, secondName, field) in Spreads)
        {
            texts.TryGetValue(firstName.Trim(), out TextMeshProUGUI first);
            texts.TryGetValue(secondName.Trim(), out TextMeshProUGUI second);
            if (first == null || second == null)
            {
                log.Add("Missing " + (first == null ? firstName : secondName));
                continue;
            }

            Prepare(first, BodyMargin);
            Prepare(second, BodyMargin);
            first.linkedTextComponent = null;
            second.linkedTextComponent = null;
            first.overflowMode = TextOverflowModes.Truncate;
            second.overflowMode = TextOverflowModes.Truncate;
            first.text = string.Empty;
            second.text = string.Empty;
            Save(first);
            Save(second);
            SerializedProperty next = transcribeSo != null ? transcribeSo.FindProperty(field) : null;
            if (next != null)
                next.objectReferenceValue = second;
            log.Add(firstName + " continues on " + secondName);
        }

        foreach (string name in Headers)
        {
            if (!texts.TryGetValue(name.Trim(), out TextMeshProUGUI header))
                continue;
            Prepare(header, HeaderMargin);
            header.overflowMode = TextOverflowModes.Overflow;
            header.text = string.Empty;
            Save(header);
        }
        log.Add("Headers kept inside the page.");

        if (transcribeSo != null)
        {
            SerializedProperty name = transcribeSo.FindProperty("_assignedNameCurrent");
            if (name != null && texts.TryGetValue("[Current]Poem_Name_Text (TMP)", out TextMeshProUGUI title))
                name.objectReferenceValue = title;
            transcribeSo.ApplyModifiedProperties();
            log.Add("TranscribePoem: title and right pages linked.");
        }
        else
        {
            log.Add("TranscribePoem not found in the open scene.");
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Fix Diary Pages\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Fix Diary Pages", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static void CenterPages(List<string> log)
    {
        Book book = Object.FindObjectsByType<Book>(FindObjectsInactive.Include).FirstOrDefault(b => !EditorUtility.IsPersistent(b));
        Canvas canvas = book != null ? book.GetComponentInParent<Canvas>(true) : null;
        if (canvas != null)
            canvas = canvas.rootCanvas;
        Transform diary = canvas != null ? canvas.transform.parent : null;
        MeshFilter mesh = diary != null ? diary.GetComponent<MeshFilter>() : null;
        if (mesh == null || mesh.sharedMesh == null)
        {
            log.Add("Diary pages: diary mesh not found, pages not centered.");
            return;
        }

        Bounds bounds = mesh.sharedMesh.bounds;
        RectTransform rect = (RectTransform)canvas.transform;
        Undo.RecordObject(rect, "Fix Diary Pages");
        Vector3 local = rect.localPosition;
        float bookOffset = diary.InverseTransformPoint(book.transform.position).y - local.y;
        float lift = 0.004f / Mathf.Max(0.0001f, diary.lossyScale.y);
        rect.localPosition = new Vector3(bounds.center.x, bounds.max.y + lift - bookOffset, bounds.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        log.Add("Diary pages centered on the diary.");
    }

    private static void Prepare(TextMeshProUGUI text, Vector4 margin)
    {
        Undo.RecordObject(text, "Fix Diary Pages");
        Undo.RecordObject(text.rectTransform, "Fix Diary Pages");
        text.textWrappingMode = TextWrappingModes.Normal;
        text.margin = margin;

        RectTransform rect = text.rectTransform;
        if (rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one)
        {
            rect.offsetMin = Vector2.Max(rect.offsetMin, Vector2.zero);
            rect.offsetMax = Vector2.Min(rect.offsetMax, Vector2.zero);
        }
    }

    private static void Save(TextMeshProUGUI text)
    {
        text.ForceMeshUpdate();
        EditorUtility.SetDirty(text);
        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        PrefabUtility.RecordPrefabInstancePropertyModifications(text.rectTransform);
    }
}
