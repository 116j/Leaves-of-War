using System;
using System.Linq;
using Hortensia.Narrative;
using UnityEditor;
using UnityEngine;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// Unity's default Inspector has no built-in type picker for a
    /// [SerializeReference] polymorphic list like ChapterDefinition/
    /// EndingDefinition's Beats - pressing "+" just inserts a null entry,
    /// and "Duplicate Array Element" can leave two entries sharing the same
    /// underlying managed reference. This window adds a beat of a chosen
    /// concrete NarrativeBeat subtype directly, as a genuinely new instance.
    /// </summary>
    public sealed class AddNarrativeBeatWindow : EditorWindow
    {
        private UnityEngine.Object targetAsset;
        private Type[] beatTypes;
        private string[] typeNames;
        private int selectedTypeIndex;
        private int insertIndex = -1;
        private int inspectIndex;
        private string inspectedTypeResult;

        private Type[] effectTypes;
        private string[] effectTypeNames;
        private int selectedEffectTypeIndex;
        private int selectedEffectListIndex;
        private int effectBeatIndex;
        private int effectInsertIndex = -1;

        [MenuItem("Hortensia/Add Narrative Beat")]
        private static void Open()
        {
            var window = GetWindow<AddNarrativeBeatWindow>("Add Beat");
            window.minSize = new Vector2(420f, 180f);
            window.RefreshTypes();
            window.RefreshEffectTypes();

            if (Selection.activeObject is ChapterDefinition || Selection.activeObject is EndingDefinition)
                window.targetAsset = Selection.activeObject;
        }

        private void RefreshTypes()
        {
            beatTypes = TypeCache.GetTypesDerivedFrom<NarrativeBeat>()
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name)
                .ToArray();
            typeNames = beatTypes.Select(t => t.Name).ToArray();
        }

        private void RefreshEffectTypes()
        {
            effectTypes = TypeCache.GetTypesDerivedFrom<StateEffect>()
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name)
                .ToArray();
            effectTypeNames = effectTypes.Select(t => t.Name).ToArray();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Unity's default Inspector can't offer a type picker for a " +
                "[SerializeReference] list like Beats on its own - this window " +
                "inserts a beat of the chosen concrete type as a genuinely new " +
                "instance (not a shared reference).",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            targetAsset = EditorGUILayout.ObjectField(
                "Chapter or Ending", targetAsset, typeof(UnityEngine.Object), false);

            if (beatTypes == null || beatTypes.Length == 0)
                RefreshTypes();

            selectedTypeIndex = Mathf.Clamp(selectedTypeIndex, 0, Mathf.Max(0, typeNames.Length - 1));
            selectedTypeIndex = EditorGUILayout.Popup("Beat Type", selectedTypeIndex, typeNames);

            SerializedObject serializedTarget = GetTargetSerializedObject();
            SerializedProperty beatsProperty = serializedTarget?.FindProperty("beats");

            int count = beatsProperty != null ? beatsProperty.arraySize : 0;
            EditorGUILayout.LabelField("Current beat count", count.ToString());

            insertIndex = EditorGUILayout.IntField("Insert At Index (-1 = append at end)", insertIndex);

            EditorGUILayout.Space(6f);
            bool canAdd = beatsProperty != null && beatTypes != null && beatTypes.Length > 0;
            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("Add Beat", GUILayout.Height(30f)))
                    AddBeat(serializedTarget, beatsProperty);
            }

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Identify an existing beat's type", EditorStyles.boldLabel);
            inspectIndex = EditorGUILayout.IntField("Beat Index", inspectIndex);
            using (new EditorGUI.DisabledScope(beatsProperty == null))
            {
                if (GUILayout.Button("Show Type", GUILayout.Height(24f)))
                    ShowBeatType(beatsProperty);
            }
            if (!string.IsNullOrEmpty(inspectedTypeResult))
                EditorGUILayout.HelpBox(inspectedTypeResult, MessageType.None);

            if (targetAsset != null && beatsProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "Selected asset isn't a ChapterDefinition or EndingDefinition (no 'beats' list found).",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Add a State Effect (inside a beat's On Complete)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "On Complete (List<StateEffect>) lives on the base NarrativeBeat class, " +
                "nested inside each beat element - so it has the exact same " +
                "missing-type-picker problem as Beats itself, one level deeper.",
                MessageType.Info);

            if (effectTypes == null || effectTypes.Length == 0)
                RefreshEffectTypes();

            effectBeatIndex = EditorGUILayout.IntField("Beat Index (whose effect list)", effectBeatIndex);
            selectedEffectListIndex = EditorGUILayout.Popup("Effect List", selectedEffectListIndex, EffectListNames);
            selectedEffectTypeIndex = Mathf.Clamp(selectedEffectTypeIndex, 0, Mathf.Max(0, effectTypeNames.Length - 1));
            selectedEffectTypeIndex = EditorGUILayout.Popup("Effect Type", selectedEffectTypeIndex, effectTypeNames);
            effectInsertIndex = EditorGUILayout.IntField("Insert At Index (-1 = append at end)", effectInsertIndex);

            string effectFieldName = EffectListFieldNames[selectedEffectListIndex];
            SerializedProperty onCompleteProperty = GetEffectListProperty(beatsProperty, effectBeatIndex, effectFieldName);
            int onCompleteCount = onCompleteProperty != null ? onCompleteProperty.arraySize : 0;
            EditorGUILayout.LabelField($"Current {EffectListNames[selectedEffectListIndex]} count", onCompleteCount.ToString());

            bool canAddEffect = onCompleteProperty != null && effectTypes != null && effectTypes.Length > 0;
            using (new EditorGUI.DisabledScope(!canAddEffect))
            {
                if (GUILayout.Button($"Add State Effect to {EffectListNames[selectedEffectListIndex]}", GUILayout.Height(30f)))
                    AddStateEffect(serializedTarget, onCompleteProperty);
            }

            if (beatsProperty != null && onCompleteProperty == null)
            {
                EditorGUILayout.HelpBox(
                    $"Beat Index {effectBeatIndex} is out of range (0-{beatsProperty.arraySize - 1}).",
                    MessageType.Warning);
            }
        }

        private static readonly string[] EffectListNames = { "On Start", "On Complete" };
        private static readonly string[] EffectListFieldNames = { "onStart", "onComplete" };

        private static SerializedProperty GetEffectListProperty(SerializedProperty beatsProperty, int beatIndex, string fieldName)
        {
            if (beatsProperty == null || beatIndex < 0 || beatIndex >= beatsProperty.arraySize)
                return null;

            SerializedProperty beatElement = beatsProperty.GetArrayElementAtIndex(beatIndex);
            return beatElement.FindPropertyRelative(fieldName);
        }

        private void AddStateEffect(SerializedObject serializedTarget, SerializedProperty onCompleteProperty)
        {
            Type chosenType = effectTypes[selectedEffectTypeIndex];
            object instance = Activator.CreateInstance(chosenType);

            int targetIndex = effectInsertIndex < 0 || effectInsertIndex > onCompleteProperty.arraySize
                ? onCompleteProperty.arraySize
                : effectInsertIndex;

            onCompleteProperty.InsertArrayElementAtIndex(targetIndex);
            SerializedProperty newElement = onCompleteProperty.GetArrayElementAtIndex(targetIndex);
            newElement.managedReferenceValue = instance;

            serializedTarget.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetAsset);
        }

        private SerializedObject GetTargetSerializedObject()
        {
            if (targetAsset == null)
                return null;
            if (!(targetAsset is ChapterDefinition) && !(targetAsset is EndingDefinition))
                return null;
            return new SerializedObject(targetAsset);
        }

        private void ShowBeatType(SerializedProperty beatsProperty)
        {
            if (inspectIndex < 0 || inspectIndex >= beatsProperty.arraySize)
            {
                inspectedTypeResult = $"Index {inspectIndex} is out of range (0-{beatsProperty.arraySize - 1}).";
                return;
            }

            SerializedProperty element = beatsProperty.GetArrayElementAtIndex(inspectIndex);
            // managedReferenceFullTypename is exactly "AssemblyName TypeFullName" -
            // this is Unity's own record of the concrete type, independent of
            // whatever the Inspector foldout happens to display.
            string fullTypeName = element.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(fullTypeName))
            {
                inspectedTypeResult = $"Element {inspectIndex} is empty (null) - it has no type assigned yet.";
                return;
            }

            int lastDot = fullTypeName.LastIndexOf('.');
            string shortName = lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
            inspectedTypeResult = $"Element {inspectIndex} is a: {shortName}\n({fullTypeName})";
        }

        private void AddBeat(SerializedObject serializedTarget, SerializedProperty beatsProperty)
        {
            Type chosenType = beatTypes[selectedTypeIndex];
            object instance = Activator.CreateInstance(chosenType);

            int targetIndex = insertIndex < 0 || insertIndex > beatsProperty.arraySize
                ? beatsProperty.arraySize
                : insertIndex;

            beatsProperty.InsertArrayElementAtIndex(targetIndex);
            SerializedProperty newElement = beatsProperty.GetArrayElementAtIndex(targetIndex);
            // Assigning a freshly-created instance here (rather than
            // duplicating an existing element) guarantees this is its own
            // managed reference, not one shared with anything else.
            newElement.managedReferenceValue = instance;

            serializedTarget.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetAsset);
            Selection.activeObject = targetAsset;
        }
    }
}