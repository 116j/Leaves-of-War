using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hortensia.Narrative;
using UnityEditor;
using UnityEngine;

// Draws a line-content struct's fields inline, so nesting one inside a beat
// costs no foldout and no extra expand-level in the chapter list.
public abstract class FlatContentDrawer : PropertyDrawer
{
    private const float GenerateButtonWidth = 76f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = 0f;
        bool any = false;

        foreach (SerializedProperty child in Children(property))
        {
            if (any)
                height += EditorGUIUtility.standardVerticalSpacing;

            height += EditorGUI.GetPropertyHeight(child, true);
            any = true;
        }

        return any ? height : EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        bool any = false;

        foreach (SerializedProperty child in Children(property))
        {
            if (any)
                y += EditorGUIUtility.standardVerticalSpacing;

            float height = EditorGUI.GetPropertyHeight(child, true);
            Rect childRect = new Rect(position.x, y, position.width, height);
            if (child.name == "lineId" && child.propertyType == SerializedPropertyType.String)
                DrawLineId(childRect, property, child);
            else
                EditorGUI.PropertyField(childRect, child, true);

            y += height;
            any = true;
        }

        if (!any)
            EditorGUI.PropertyField(position, property, label, true);

        EditorGUI.EndProperty();
    }

    private static void DrawLineId(
        Rect position,
        SerializedProperty contentProperty,
        SerializedProperty lineIdProperty)
    {
        Rect fieldRect = position;
        fieldRect.width = Mathf.Max(0f, position.width - GenerateButtonWidth - 4f);
        EditorGUI.PropertyField(fieldRect, lineIdProperty, true);

        string proposal = BuildLineIdProposal(contentProperty);
        Rect buttonRect = position;
        buttonRect.xMin = fieldRect.xMax + 4f;
        bool hasAuthoredId = !string.IsNullOrEmpty(lineIdProperty.stringValue);
        var buttonLabel = new GUIContent(
            "Generate",
            string.IsNullOrEmpty(proposal) ? "No line ID proposal is available." : $"Proposed ID: {proposal}");

        using (new EditorGUI.DisabledScope(hasAuthoredId || string.IsNullOrEmpty(proposal)))
        {
            if (!GUI.Button(buttonRect, buttonLabel))
                return;

            // Re-check at the point of mutation. A generated proposal is only a
            // convenience; an authored ID is never replaced.
            if (!string.IsNullOrEmpty(lineIdProperty.stringValue))
                return;

            lineIdProperty.stringValue = proposal;
            lineIdProperty.serializedObject.ApplyModifiedProperties();
        }
    }

    private static string BuildLineIdProposal(SerializedProperty contentProperty)
    {
        UnityEngine.Object target = contentProperty.serializedObject.targetObject;
        string speakerSlug = SpeakerSlug(contentProperty);

        if (target is ChapterDefinition chapter &&
            TryExtractBeatIndex(contentProperty.propertyPath, out int chapterBeatIndex))
        {
            int sequence = PresentedLinesBefore(chapter.Beats, chapterBeatIndex) + 1;
            return FormatChapterLineId(chapter.Index, sequence, speakerSlug);
        }

        if (target is PatientDefinition patient &&
            TryFindPatientLocation(patient, out ChapterDefinition patientChapter, out int patientSequence))
        {
            return FormatChapterLineId(
                patientChapter.Index,
                patientSequence,
                Slug(patient.DisplayName));
        }

        if (target is EndingDefinition ending &&
            TryExtractBeatIndex(contentProperty.propertyPath, out int endingBeatIndex))
        {
            if (TryFindEndingLocation(
                    ending,
                    endingBeatIndex,
                    out int endingChapterIndex,
                    out int endingSequence))
            {
                return FormatChapterLineId(endingChapterIndex, endingSequence, speakerSlug);
            }

            string endingSlug = Slug(!string.IsNullOrWhiteSpace(ending.Id) ? ending.Id : ending.name);
            int localSequence = PresentedLinesBefore(ending.Beats, endingBeatIndex) + 1;
            return $"ending_{endingSlug}_{localSequence:000}_{speakerSlug}";
        }

        string assetName = target != null ? target.name : string.Empty;
        if (string.IsNullOrWhiteSpace(assetName) && target != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(target);
            assetName = Path.GetFileNameWithoutExtension(assetPath);
        }

        string assetSlug = Slug(assetName);
        return string.IsNullOrEmpty(assetSlug) ? string.Empty : $"line_{assetSlug}_{speakerSlug}";
    }

    private static string SpeakerSlug(SerializedProperty contentProperty)
    {
        string path = contentProperty.propertyPath;
        int separator = path.LastIndexOf('.');
        if (separator >= 0)
        {
            SerializedProperty owner = contentProperty.serializedObject.FindProperty(path.Substring(0, separator));
            SerializedProperty speakerProperty = owner?.FindPropertyRelative("speaker");
            if (speakerProperty?.objectReferenceValue is SpeakerDefinition speaker)
            {
                string speakerName = !string.IsNullOrWhiteSpace(speaker.Id)
                    ? speaker.Id
                    : !string.IsNullOrWhiteSpace(speaker.DisplayName)
                        ? speaker.DisplayName
                        : speaker.name;
                string slug = Slug(speakerName);
                if (!string.IsNullOrEmpty(slug))
                    return slug;
            }
        }

        return "speaker";
    }

    private static bool TryFindPatientLocation(
        PatientDefinition patient,
        out ChapterDefinition chapter,
        out int sequence)
    {
        chapter = null;
        sequence = 0;

        string[] chapterGuids = AssetDatabase.FindAssets("t:ChapterDefinition");
        var chapters = new List<ChapterDefinition>(chapterGuids.Length);
        for (int i = 0; i < chapterGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(chapterGuids[i]);
            ChapterDefinition candidate = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);
            if (candidate != null)
                chapters.Add(candidate);
        }
        chapters.Sort((left, right) => left.Index.CompareTo(right.Index));

        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            ChapterDefinition candidateChapter = chapters[chapterIndex];
            int linesBeforeBeat = 0;
            for (int beatIndex = 0; beatIndex < candidateChapter.Beats.Count; beatIndex++)
            {
                NarrativeBeat beat = candidateChapter.Beats[beatIndex];
                if (beat is PatientSessionBeat session)
                {
                    for (int patientIndex = 0; patientIndex < session.Roster.Count; patientIndex++)
                    {
                        if (session.Roster[patientIndex] != patient)
                            continue;

                        chapter = candidateChapter;
                        sequence = linesBeforeBeat + patientIndex + 1;
                        return true;
                    }

                    linesBeforeBeat += session.Roster.Count;
                }
                else if (beat is LineBeat)
                {
                    linesBeforeBeat++;
                }
            }
        }

        return false;
    }

    private static bool TryFindEndingLocation(
        EndingDefinition ending,
        int beatIndex,
        out int chapterIndex,
        out int sequence)
    {
        chapterIndex = 0;
        sequence = 0;

        string[] catalogGuids = AssetDatabase.FindAssets("t:NarrativeCatalog");
        for (int catalogIndex = 0; catalogIndex < catalogGuids.Length; catalogIndex++)
        {
            string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[catalogIndex]);
            NarrativeCatalog catalog = AssetDatabase.LoadAssetAtPath<NarrativeCatalog>(catalogPath);
            if (catalog == null || catalog.Chapters.Count == 0)
                continue;

            ChapterDefinition finalChapter = null;
            for (int i = 0; i < catalog.Chapters.Count; i++)
            {
                ChapterDefinition candidate = catalog.Chapters[i];
                if (candidate != null && (finalChapter == null || candidate.Index > finalChapter.Index))
                    finalChapter = candidate;
            }
            if (finalChapter == null)
                continue;

            int linesBeforeEnding = CountPresentedLines(finalChapter.Beats);
            for (int i = 0; i < catalog.Endings.Count; i++)
            {
                EndingDefinition candidate = catalog.Endings[i];
                if (candidate == ending)
                {
                    chapterIndex = finalChapter.Index;
                    sequence = linesBeforeEnding + PresentedLinesBefore(ending.Beats, beatIndex) + 1;
                    return true;
                }

                if (candidate != null)
                    linesBeforeEnding += CountPresentedLines(candidate.Beats);
            }
        }

        return false;
    }

    private static int PresentedLinesBefore(IReadOnlyList<NarrativeBeat> beats, int beatIndex)
    {
        int count = 0;
        int limit = Mathf.Clamp(beatIndex, 0, beats.Count);
        for (int i = 0; i < limit; i++)
            count += PresentedLineCount(beats[i]);
        return count;
    }

    private static int CountPresentedLines(IReadOnlyList<NarrativeBeat> beats)
    {
        int count = 0;
        for (int i = 0; i < beats.Count; i++)
            count += PresentedLineCount(beats[i]);
        return count;
    }

    private static int PresentedLineCount(NarrativeBeat beat)
    {
        if (beat is LineBeat)
            return 1;
        if (beat is PatientSessionBeat session)
            return session.Roster.Count;
        return 0;
    }

    private static bool TryExtractBeatIndex(string propertyPath, out int beatIndex)
    {
        const string marker = "beats.Array.data[";
        beatIndex = -1;
        int start = propertyPath.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return false;

        start += marker.Length;
        int end = propertyPath.IndexOf(']', start);
        return end > start && int.TryParse(propertyPath.Substring(start, end - start), out beatIndex);
    }

    private static string FormatChapterLineId(int chapterIndex, int sequence, string speakerSlug) =>
        $"ch{Mathf.Max(0, chapterIndex):00}_{Mathf.Max(1, sequence):000}_{speakerSlug}";

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        bool separatorPending = false;
        for (int i = 0; i < value.Length; i++)
        {
            char character = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                    builder.Append('_');
                builder.Append(character);
                separatorPending = false;
            }
            else
            {
                separatorPending = builder.Length > 0;
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<SerializedProperty> Children(SerializedProperty property)
    {
        SerializedProperty end = property.GetEndProperty();
        SerializedProperty iterator = property.Copy();

        if (!iterator.NextVisible(true))
            yield break;

        do
        {
            if (SerializedProperty.EqualContents(iterator, end))
                yield break;

            yield return iterator.Copy();
        }
        while (iterator.NextVisible(false));
    }
}

[CustomPropertyDrawer(typeof(LineContent))]
public sealed class LineContentDrawer : FlatContentDrawer
{
}

[CustomPropertyDrawer(typeof(NameVariantLineContent))]
public sealed class NameVariantLineContentDrawer : FlatContentDrawer
{
}
